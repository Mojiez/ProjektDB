﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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
        private static User logedInUser;

        public UserController(IConfiguration configuration)
        {
            this.configuration = configuration;
            db.SetConnection(configuration.GetConnectionString("SkpDb"));
        }

        // login needs to handle failed logins
        public IActionResult UserLogin(string loginName, string password)
        {
            if (loginName == null)
                return BadRequest();
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
                        logedInUser = db.GetUser(loginName);
                        logedInUser = db.GetUserRoles(logedInUser);
                        if (logedInUser.Roles.Contains(Roles.Instruktør))
                            return Redirect("/Project/ProjectOverView");
                        else
                            return Redirect("/User/UserOverView");
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
            //returns a list of User models
            List<User> users = db.GetAllUsers();

            users.Where(x => x.Login == logedInUser.Login).Select(x => x.Owner = users.IndexOf(x));
            if(logedInUser.Roles.Contains(Roles.Instruktør))
            {
                users.Where(x => x.Login == logedInUser.Login).Select(x => x.Admin = true);
            }

            return View(users);
        }

        public IActionResult SingleUserView(string userName)
        {
            User user = db.GetAllUsers().Where(x => x.Login == userName).FirstOrDefault();
            user = db.GetUserRoles(user);

            if(logedInUser != null && !string.IsNullOrEmpty(logedInUser.Login))
            {
                if (user.Roles.Contains(Roles.Instruktør))
                {
                    user.Admin = true;
                    return View(user);
                }
                else if (user.Login == logedInUser.Login)
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
        /// This is used to get user from Db
        /// </summary>
        /// <returns></returns>
        public User ReadUser(string username)
        {
            return db.GetUser(username);
        }

        /// <summary>
        /// This is used to update user data
        /// </summary>
        /// <returns></returns>
        public IActionResult UpdateUser(User user)
        {
            var users = db.GetAllUsers();

            foreach (var userInList in users)
            {
                if (userInList == user)
                {
                    return View(user);
                }
                else
                {
                    return View(db.GetUser(user.Login));
                }
            }

            return View();
        }


        //-------------------------------------------------------------- vv Admin only views vv

        /// <summary>
        /// This is used to save newly created users
        /// </summary>
        /// <returns></returns>
        public IActionResult CreateUser(User user)
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
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 10; i++)
                {
                    sb.Append(random.Next(0, 10));
                }
                user.Hash = security.Hash(Convert.FromBase64String(security.Encrypt(Encoding.UTF8.GetBytes(sb.ToString()), Convert.FromBase64String(user.Salt))));

                // create user on database
                db.CreateUser(user);
                return Redirect("/User/UserOverView");
            }
        }

        [HttpGet]
        /// <summary>
        /// This is used to delete users from Db
        /// </summary>
        /// <returns></returns>
        public IActionResult DeleteUser(User user)
        {
            user.Admin = true;
            return View(user);
        }

        [HttpPost]
        public IActionResult DeleteUser(string username)
        {
            db.DeleteUser(db.GetUser(username));
            return Redirect("/User/UserOverView");
        }

        public IActionResult AddRoleToUser(string userName, string role)
        {
            User user = db.GetUser(userName);
            user = db.GetUserRoles(user);
            if (string.IsNullOrEmpty(role))
            {
                user.Admin = true;
                return View(user);
            }
            else
            {
                //Save role to db
                foreach (Roles roles in (Roles[]) Enum.GetValues(typeof(Roles)))
                {
                    if (role == roles.ToString())
                        user.Roles.Add(roles);
                }

                db.AddRoleToUser(user);
                return View("SingleUserView", user);
            }
        }
    }
}
