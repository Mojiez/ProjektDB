﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Skp_ProjektDB.Backend.Managers;
using Skp_ProjektDB.Models;
using Skp_ProjektDB.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Skp_ProjektDB.Controllers
{
    public class UserController : Controller
    {
        private readonly IConfiguration configuration;
        private Backend.Managers.Db db = new Backend.Managers.Db();
        private Backend.Managers.Security security = new Backend.Managers.Security();
        public static UserLogin logedInUser { get; set; }

        public UserController(IConfiguration configuration)
        {
            if (logedInUser == null)
            {
                logedInUser = new UserLogin(db);
            }
            this.configuration = configuration;
            db.SetConnection(configuration.GetConnectionString("SkpDb"));
        }

        // login needs to handle failed logins
        [ValidateAntiForgeryToken]
        public IActionResult UserLogin(string loginName, string password)
        {
            
            if (loginName == null)
                return BadRequest("Login oplysningerne var ikke korrekt");
            else
            {
                // gets salt if username exists
                string salt = db.GetSalt(loginName);
                if (salt != null)
                {
                    // gets the hash value of typed password
                    string encrypted = security.Encrypt(Encoding.UTF8.GetBytes(password), Convert.FromBase64String(salt));
                    string hash = security.Hash(Convert.FromBase64String(encrypted));

                    if (hash == db.GetHash(loginName)) // if hashes matches == password is correct
                    {
                        // gets the user who logged in
                        logedInUser.User = db.GetUserByUserName(loginName);
                        db.GetUserRoles(logedInUser.User);

                        if (logedInUser.User.UserRoles.Contains(Skp_ProjektDB.Models.User.Roles.Instruktør))
                        {
                            return Redirect("/Project/ProjectOverView");
                        }
                        else
                        {
                            return Redirect("/User/UserOverView");
                        }
                    }
                    else
                    {
                        // password is wrong
                        return BadRequest("Login oplysningerne var ikke korrekt");
                    }
                }
                else
                {
                    // username is incorrect!
                    return BadRequest("Login oplysningerne var ikke korrekt");
                }
            }
        }

        /// <summary>
        /// Makes a detailed view of all users in db
        /// </summary>
        /// <returns></returns>
        public IActionResult UserOverView()
        {
            if (logedInUser != null)
            {
                //returns a list of User models
                List<User> users = db.GetAllUsers();

                users.Where(x => x.Login == logedInUser.User.Login).Select(x => x.Owner = users.IndexOf(x));
                if (logedInUser.User.UserRoles.Contains(Skp_ProjektDB.Models.User.Roles.Instruktør))
                {
                    users.Where(x => x.Login == logedInUser.User.Login).Select(x => x.Admin = true);
                }
                return View(users);
            }
            else
                return BadRequest("Du er ikke logget ind");
        }

        public IActionResult SingleUserView(int userID)
        {
            User user = null;
            if (userID != 0)
            {
                user = db.GetAllUsers().Where(x => x.Id == userID).FirstOrDefault();

                var projects = db.GetAllProjects();
                foreach (var item in projects)
                {
                    item.Team = db.GetTeam(item.Id);
                }
            }

            if (logedInUser != null && !string.IsNullOrEmpty(logedInUser.User.Login))
            {
                if (user.UserRoles.Contains(Skp_ProjektDB.Models.User.Roles.Instruktør))
                {
                    user.Admin = true;
                    return View(user);
                }
                else if (user.Login == logedInUser.User.Login)
                {
                    user.Owner = 1;
                    return View(user);
                }
                else
                {
                    return View(user);
                }
            }
            else
            {
                return BadRequest("Du er ikke logget ind");
            }
        }

        [HttpPost]
        public IActionResult UserSearch(string searchWord)
        {
            List<User> users;
            if (searchWord == null)
            {
                users = db.GetAllUsers();
            }
            else
            {
                users = db.GetAllUsers().FindAll(x => x.Name.ToLower().Contains(searchWord.ToLower()));
            }
            return View("UserOverView", users);
        }

        //------------------------------------------------------------ vv CRUD Views vv
        /// <summary>
        /// This is used to update user data
        /// </summary>
        /// <returns></returns>
        public IActionResult UpdateUser(int userID)
        {
            //This method will only be available to Admin or the user that owns the account 
            var users = db.GetAllUsers();
            if (logedInUser != null)
            {
                foreach (var userInList in users)
                {
                    if (userInList.Id == userID)
                    {
                        userInList.Owner = 1;
                        return View(userInList);
                    }
                    else if (userID == userInList.Id)
                    {
                        return View(db.GetUserById(userID));
                    }
                }
                return BadRequest("Fandt ikke noget!");
            }
            else
                return BadRequest("Du er ikke logget ind");
        }

        //-------------------------------------------------------------- vv Admin only views vv

        /// <summary>
        /// This is used to save newly created users
        /// </summary>
        /// <returns></returns>
        public IActionResult CreateUser(User user)
        {
            if (logedInUser != null)
            {
                if (user.Name == null)
                {
                    user.Admin = true;
                    return View(user);
                }
                else
                {
                    // create salt for user
                    user.Salt = security.GenerateSalt();

                    // create random password for user
                    Random random = new Random();
                    user.Hash = security.Hash(Convert.FromBase64String(security.Encrypt(Encoding.UTF8.GetBytes("Kode1234"), Convert.FromBase64String(user.Salt))));

                    // create user on database
                    db.CreateUser(user);
                    return Redirect("/User/UserOverView");
                }
            }
            else
                return BadRequest("Du er ikke logget ind");
        }

        //
        //Change the way Delete An update works 
        //Return list with all users and buttons to delete and update 
        //

        /// <summary>
        /// This is used to delete users from Db
        /// </summary>
        /// <returns></returns>
        public void DeleteUser(int userID)
        {
            //if (logedInUser != null)
            //{
            //    db.DeleteUser();
            //}
            //Redirect("/User/UserOverView");
        }

        [HttpPost]
        public IActionResult AddRoleToUser(string userName, string role)
        {
            if (logedInUser != null)
            {
                User user = db.GetUserByUserName(userName);
                user = db.GetUserRoles(user);
                if (string.IsNullOrEmpty(role))
                {
                    user.Admin = true;
                    return View(user);
                }
                else
                {
                    //Save role to db
                    foreach (User.Roles roles in (User.Roles[])Enum.GetValues(typeof(User.Roles)))
                    {
                        if (role == roles.ToString())
                            user.UserRoles.Add(roles);
                    }

                    db.AddRoleToUser(user);
                    return View("SingleUserView", user);
                }
            }
            else
                return BadRequest("Du er ikke logget ind");
        }

        public IActionResult RemoveRoleFromUser()
        {
            return View();
        }
    }
}
