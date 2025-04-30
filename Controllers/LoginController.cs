using CRUD_OperationByMeUsingJqueryAjaxMvc.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace CRUD_OperationByMeUsingJqueryAjaxMvc.Controllers
{
    public class LoginController : Controller
    {
        // GET: Login

        Dhigurah_DBEntities db = new Dhigurah_DBEntities();
        

        public ActionResult Login(string Result)
        {
            return View();
        }
        public List<String> GetCCList()
        {
            var ccList = db.mks_SysUsers.Where(x => x.UserName != "Infox" && x.IsBlock == false).Select(x => x.Email).ToList();
            return ccList;
        }
        public JsonResult CheckLogin(POR_User model)
        {
            bool result = false;
            string notification = "";
            string password = "";
            bool isFirstAttempt = false;
            try
            {
                password = UserSecurityController.MD5Hash(model.Password).ToString();

                POR_User user = db.POR_User.Where(x => x.UserName == model.UserName && x.Password == password).FirstOrDefault();
                if (user != null)
                {
                    if (user.IsActive == false || db.Employees.Where(x => x.EmployeeId == user.EmployeeId).Select(x => x.Active).FirstOrDefault() == false)
                    {
                        result = false;
                        notification = "Inactive User.";
                    }
                    else
                    {
                        result = true;
                        Session["UserName"] = user.UserName;
                        Session["UserId"] = user.UserId;
                        Session["EmployeeId"] = user.EmployeeId;
                        Session["UserTypeId"] = user.UserTypeId;
                        Session["IsFirstAttempt"] = user.IsFirstAttempt;
                        Session["CompanyId"] = db.Employees.Where(x => x.EmployeeId == user.EmployeeId).Select(x => x.CompanyID).FirstOrDefault();
                        Session["FirstName"] = db.Employees.Where(x => x.EmployeeId == user.EmployeeId).Select(x => x.FirstName).FirstOrDefault();
                        //Session["CenterStaff"]=db.Employees.Where(x=>x.EmployeeId==user.EmployeeId).Select(x=>x.CenterStaff).FirstOrDefault();
                        isFirstAttempt = user.IsFirstAttempt;
                    }
                }
                else
                {
                    result = false;
                    notification = "Login Failed.";
                }
            }
            catch(Exception ex)
            {
                notification = ex.Message;
            }
            

            return Json(new { success = result, message = notification, firstattempt = isFirstAttempt}, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Logout()
        {
            Session["UserName"] = null;
            return RedirectToAction("Login", "Login");
        }

        public void CheckIsFirstAttempt()
        {
            int userId = Convert.ToInt32(Session["UserId"]);
            var isFirstAttempt = db.POR_User.Where(x => x.UserId == userId).Select(x => new { x.IsFirstAttempt, x.IsResetPassword }).FirstOrDefault();
           // if (isFirstAttempt.IsFirstAttempt || isFirstAttempt.IsResetPassword)
            {
           //     Response.Redirect("/UserSecurity/ChangePassword");
            }
        }

        public JsonResult ResetPassword(string UserName)
        {
            bool result = false;
            string notification = "";
            bool isFirstAttempt = false;
            try
            {
                POR_User user = db.POR_User.Where(x => x.UserName == UserName).FirstOrDefault();
                if (user != null)
                {
                    var empObj = db.Employees.Where(x => x.EmployeeId == user.EmployeeId).Select(x => new { x.Email, x.FirstName }).FirstOrDefault();
                    if (string.IsNullOrEmpty(empObj.Email))
                    {
                        result = false;
                        notification = "No email found for user. Please contact admin.";
                    }
                    else
                    {
                        Random rnd = new Random();
                        string password = UserName + rnd.Next(1, 100000).ToString();
                        SendEmailForPasswordReset(empObj.FirstName, password, empObj.Email);
                        user.Password = MD5Hash(password);
                        user.IsResetPassword = true;
                        user.UpdatedDate = DateTime.Now;
                        user.UpdatedUser = "System";
                        db.SaveChanges();
                        result = true;
                        notification = "Password reset successfully. Please login with temporary password and reset the password.";
                    }
                }
                else
                {
                    result = false;
                    notification = "No user found for entered user name.";
                }
            }
            catch (Exception ex)
            {
                notification = ex.Message;
            }


            return Json(new { success = result, message = notification, firstattempt = isFirstAttempt }, JsonRequestBehavior.AllowGet);
        }

        public void SendEmailForPasswordReset(string EmployeeName, string Password, string ToEmail)
        {
            StringWriter writer = new StringWriter();
            HtmlTextWriter html = new HtmlTextWriter(writer);
            string senderEmail = ConfigurationManager.AppSettings["LeaveEmail"];

            Style style = new Style();
            style.Font.Name = "Verdana";
            style.Font.Size = 10;
            style.Font.Bold = false;
            html.EnterStyle(style);
            html.RenderBeginTag(HtmlTextWriterTag.P);
            html.WriteEncodedText(string.Format("Dear {0},", EmployeeName));
            html.WriteBreak();
            html.RenderBeginTag(HtmlTextWriterTag.P);

            html.WriteEncodedText(string.Format("You have requested for a password reset."));
            html.WriteEncodedText(string.Format(" {0} will be your temporary password.", Password));
            html.WriteBreak();

            html.RenderBeginTag(HtmlTextWriterTag.P);
            html.WriteEncodedText("Thank you");
            html.WriteBreak();
            Style style1 = new Style();
            style1.Font.Name = "Verdana";
            style1.Font.Size = 10;
            style1.Font.Bold = false;
            html.EnterStyle(style1);
            html.RenderBeginTag(HtmlTextWriterTag.P);
            html.WriteEncodedText("Click Here to Log ESS Portal: http://20.212.187.239:82/");
            html.WriteBreak();
            html.WriteEncodedText("Please note that this email is automatically generated from the system, therefore, do not need to reply.");
            html.WriteBreak();
            html.Flush();
            string htmlString = writer.ToString();
            string subject = "Password Reset";

            CRUD_OperationByMeUsingJqueryAjaxMvc.Email.Email email = new CRUD_OperationByMeUsingJqueryAjaxMvc.Email.Email();
            email.SendemailInExchangeServer(senderEmail, ToEmail, subject, htmlString,GetCCList());
        }

        //To return MD5 Hashtag value for password
        public static string MD5Hash(string input)
        {
            StringBuilder hash = new StringBuilder();
            MD5CryptoServiceProvider md5provider = new MD5CryptoServiceProvider();
            byte[] bytes = md5provider.ComputeHash(new UTF8Encoding().GetBytes(input));

            for (int i = 0; i < bytes.Length; i++)
            {
                hash.Append(bytes[i].ToString("x2"));
            }
            return hash.ToString();
        }

    }
}