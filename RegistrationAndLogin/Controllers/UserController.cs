using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using RegistrationAndLogin.Models;

namespace RegistrationAndLogin.Controllers
{
    public class UserController : Controller
    {
         // 1.Registration Action
         [HttpGet]
         public ActionResult Registration()
         {
            return View();
         }

        // 2.Registraiton Post Action
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Registration([Bind(Exclude = "IsEmailVerified, ActivationCode")]User user)
        {
            bool Status = false;
            string Message = "";

            // 

            #region 1.Model Validation
            if (ModelState.IsValid)
            {

                #region 2.Email Exists?
                var isExist = IsEmailExists(user.EmailID);
                if (isExist)
                {
                    ModelState.AddModelError("EmailExist", "Email Already Exist");
                    return View(user);
                }
                #endregion

                #region 3.Generate Activation Code //ctrl+k, ctrl+s
                user.ActivationCode = Guid.NewGuid();
                #endregion

                #region 4.Password Hashing
                user.Password = Crypto.Hash(user.Password);
                user.ConfirmPassword = Crypto.Hash(user.ConfirmPassword);
                #endregion

                user.IsEmailVerified = false;

                #region Save Data To Database
                using (UserDBContext db = new UserDBContext())
                {
                    db.Users.Add(user);
                    db.SaveChanges();

                    // 6.Send Email to User
                    SendVerificationLinkEmail(user.EmailID, user.ActivationCode.ToString());
                    Message = "Registration successfully done." +
                        "Account activation link has been sent to your email id:" + user.EmailID;
                    Status = true;
                }
                #endregion
            }
            else
            {
                Message = "Invalid Request";
            }
            #endregion

            ViewBag.Message = Message;
            ViewBag.Status = Status;
            return View(user);
        }

        // 3.Verify Account

        [HttpGet]
        public ActionResult VerifyAccount(string id)
        {
            bool Status = false;
            using (UserDBContext db = new UserDBContext())
            {
                db.Configuration.ValidateOnSaveEnabled = false;
                var v = db.Users.Where(x => x.ActivationCode == new Guid(id)).FirstOrDefault();
                if(v != null)
                {
                    v.IsEmailVerified = true;
                    db.SaveChanges();
                    Status = true;
                }
                else
                {
                    ViewBag.Message = "Invalid Request";
                }
            }
            ViewBag.Status = Status;
            return View();
        }
        

        // 5.Login
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        // 6.Login Post
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(UserLogin user, string ReturnUrl = "")
        {
            string message = "";

            using (UserDBContext db = new UserDBContext())
            {
                var v = db.Users.Where(x => x.EmailID == user.EmailID).FirstOrDefault();
                if(v != null)
                {
                    if(string.Compare(Crypto.Hash(user.Password), v.Password) == 0)
                    {
                        int timeout = user.RememberMe ? 5 : 1;
                        var ticket = new FormsAuthenticationTicket(v.FirstName, user.RememberMe, timeout);
                        string encrypted = FormsAuthentication.Encrypt(ticket);
                        var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted);
                        cookie.Expires = DateTime.Now.AddMinutes(timeout);
                        cookie.HttpOnly = true;
                        Response.Cookies.Add(cookie);

                        if (Url.IsLocalUrl(ReturnUrl))
                        {
                            return Redirect(ReturnUrl);
                        }
                        else
                        {
                            return RedirectToAction("Index", "Home");
                        }
                    }
                    else
                    {
                        message = "Invalid Credential";
                    }
                }
                else
                {
                    message = "Invalid Credential";
                }

            }
            ViewBag.Message = message;
            return View();
        }

        // 7.Logout
        [Authorize]
        [HttpPost]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "User");
        }

        [NonAction]
        public bool IsEmailExists(string emailId)
        {
            using (UserDBContext db = new UserDBContext())
            {
                var check = db.Users.Where(x => x.EmailID == emailId).FirstOrDefault();
                return check != null;
            }
        }

        [NonAction]
        public void SendVerificationLinkEmail(string emailID, string activationCode, string emailFor = "VerifyAccount")
        {
            var verifyUrl = "/User/"+emailFor+"/" + activationCode;
            var link = Request.Url.AbsoluteUri.Replace(Request.Url.PathAndQuery, verifyUrl);

            var fromEmail = new MailAddress("snirjhorbd@gmail.com", "Registration Processing");
            var toEmail = new MailAddress(emailID);
            var fromEmailPassword = "sourav9924";

            string subject = "";
            string body = "";

            if(emailFor == "VerifyAccount")
            {
                 subject = "Your account is successfully created";
                 body = "<br/><br/>We are excited to tell you that your account is successfully created." +
                    "Please Click on the below link to verify your account" +
                    "<br/><br/><a href='" + link + "'>" + link + "</a>";
            }
            else if(emailFor == "ResetPassword")
            {
                subject = "Reset Password";
                body = "Hi,<br/><br/>We got request for reset your account password." +
                    "Please clink on the below link to reset your password" +
                    "<br/><br/><a href='"+link+"'>Reset Password Link</a>";
            }
            

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromEmail.Address, fromEmailPassword)
            };

            using (var message = new MailMessage(fromEmail, toEmail)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            })
            smtp.Send(message);
            
        }


        // 8.Forgot Password
        public ActionResult ForgotPassword()
        {
            return View();
        }

        // 9.Forgot Password Post Action
        [HttpPost]
        public ActionResult ForgotPassword(string EmailID)
        {
            // Verify Email
            // Generate Reset Password Link
            // Send Email

            string message = "";
            bool status = false;

            using (UserDBContext db = new UserDBContext())
            {
                var account = db.Users.Where(x => x.EmailID == EmailID).FirstOrDefault();
                if(account != null)
                {
                    // send email for reset password
                    string resetCode = Guid.NewGuid().ToString();
                    SendVerificationLinkEmail(account.EmailID, resetCode, "ResetPassword");
                    account.ResetPasswordCode = resetCode;

                    db.Configuration.ValidateOnSaveEnabled = false;
                    db.SaveChanges();
                    message = "Reset password link has been sent to your email id.";
                }
                else
                {
                    message = "Account not found";
                }
            }
            ViewBag.Message = message;
            return View();
        }

        // 10.Reset Password
        public ActionResult ResetPassword(string id)
        {
            // Verify the reset password link
            // Find Account associated with this link
            // Redirect to reset password page

            using (UserDBContext db = new UserDBContext())
            {
                var user = db.Users.Where(x => x.ResetPasswordCode == id).FirstOrDefault();
                if(user != null)
                {
                    ResetPasswordModel model = new ResetPasswordModel();
                    model.ResetCode = id;
                    return View(model);
                }
                else
                {
                    return HttpNotFound();
                }
            }
        }

        // 11.Reset Password Post Action
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPasswordModel model)
        {
            string message = "";
            if (ModelState.IsValid)
            {
                using (UserDBContext db = new UserDBContext())
                {
                    var user = db.Users.Where(x => x.ResetPasswordCode == model.ResetCode).FirstOrDefault();
                    if(user != null)
                    {
                        user.Password = Crypto.Hash(model.NewPassword);
                        user.ResetPasswordCode = "";
                        db.Configuration.ValidateOnSaveEnabled = false;
                        db.SaveChanges();
                        message = "New Password Updated Successfully";
                    }                   
                }
            }
            else
            {
                message = "Something Invalied";
            }

            ViewBag.Message = message;
            return View(model);
        }
    }
}