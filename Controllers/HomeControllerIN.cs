using CRUD_OperationByMeUsingJqueryAjaxMvc.Models;
using CRUD_OperationByMeUsingJqueryAjaxMvc.Models.LeaveModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;

namespace CRUD_OperationByMeUsingJqueryAjaxMvc.Controllers
{
    public class HomeController : Controller
    {
        Dhigurah_DBEntities db = new Dhigurah_DBEntities();
        UserSecurityController uscObj = new UserSecurityController();

        public ActionResult Index()
        {
            return View();
        }
        public ActionResult TestIndex()
        {
            return View();
        }
        public ActionResult JobDescription()
        {
            return View();
        }
        public FileResult DispalyFile(Int64 employeeId)
        {

            try
            {
                string fileName = "";// db.Employees.Where(x => x.EmployeeId == employeeId).Select(x => x.JdFileName).SingleOrDefault().ToString();
                string Designation = "";// db.Employees.Where(x => x.EmployeeId == employeeId).Select(x => x.DesignationID).SingleOrDefault().ToString();

                string folderPath = Server.MapPath(ConfigurationManager.AppSettings["JdFileUploadFolder"]) + "/" + Designation;

                string filePath = folderPath + "/" + fileName ;
                byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, System.Net.Mime.MediaTypeNames.Application.Octet, filePath);
                //return File(fileBytes, "application/pdf,application/JPG,application/JPGE");

            }
            catch (Exception ex)
            {
                return File("", "application/pdf");
            }

        }
        public JsonResult JobDescriptionk(Int64 EmployeeId)
        {
            EmployeeId = 11367;
            var empDetails = "";
                //db.Employees.Where(x => x.EmployeeId == EmployeeId).Join(
                //db.Designations,
                //em1=>em1.DesignationID,
                //de=>de.DesignationID,
                //(em1,de) => new { Employee = em1, Designation = de }).Join(
                //    db.HR_SaveFileDetailsJD,
                //    em => em.Employee.DesignationID,
                //    ds => ds.DesignationId,
                //    (em, ds) => new {em.Employee ,em.Designation ,HR_SaveFileDetailsJD= ds}).ToList().Select(x => new
                //    {
                //       x.Designation.DesignationName,
                //       x.HR_SaveFileDetailsJD.Duties,
                //       x.HR_SaveFileDetailsJD.RemunerationandBenifits,
                //       x.HR_SaveFileDetailsJD.Responsibilities,
                //       x.HR_SaveFileDetailsJD.Task,
                      

                //    }).ToList();

            return Json(new { dataList = empDetails }, JsonRequestBehavior.AllowGet);
        }
       
        public ActionResult DashBoard()
        {
            dynamic newModal = new ExpandoObject();
           // newModal.Employees = GetEmployeesDropDown();
            newModal.LeaveTypes = GetLeaveTypesDropDown();
            newModal.LeaveEmployees = GetLeaveEmployeesDropDown();
            newModal.HalfDayShifts = GetHalfDayShiftDropDown();
            newModal.LeaveReasons = GetLeaveResonDropDown();
            return View(newModal);
        }

        public ActionResult TestIndex2()
        {
            return View();
        }

        public ActionResult SideNav()
        {
            dynamic myModal = new ExpandoObject();
            myModal.MainMenus = GetMainMenues();
            myModal.SubMenus = GetSubMenues();
            myModal.Forms = GetForms();
            return PartialView("_sideNav", myModal);
        }

        public ActionResult GetEmployeesDropDown()
        {
            int employeeId = 0;
            int companyId = Convert.ToInt32(Session["CompanyId"]);
            if (Session["EmployeeId"] != null || Session["EmployeeId"] != "")
            {
                employeeId = Convert.ToInt32(Session["EmployeeId"]);
            }
            string userType = Session["UserTypeId"].ToString();

            var list = db.Database.SqlQuery<EmployeeDropdown>(
                   "exec dbo.[POR_GetEmployeeforLevelTwoIndex] @EmpeeId,@CompanyId,@UserType",
                    new Object[] {
                    new SqlParameter("@EmpeeId", employeeId),
                    new SqlParameter("@CompanyId", companyId),
                    new SqlParameter("@UserType", userType),
                    }).Select(x => new {

                        EmployeeIds = x.EmployeeId,
                        Name = x.EmployeeCode + "/" + x.FirstName + " " + x.LastName
                    }).Where(x => x.EmployeeIds != employeeId).ToList();

            //var empList = list.ToList().Select(x => new Employee
            //{
            //    EmployeeId = x.EmployeeId,
            //    EmployeeCode = x.EmployeeCode,
            //    FirstName = x.FirstName,
            //    LastName = x.LastName
            //}).Where(x => x.EmployeeId != employeeId).ToList();

            return Json(new { dataList = list }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetEmployeesDropDownForChange(int EmployeeId)
        {
            //int employeeId = 0;
            int companyId = Convert.ToInt32(Session["CompanyId"]);

            string userType = Session["UserTypeId"].ToString();

            var list = db.Database.SqlQuery<EmployeeDropdown>(
                   "exec dbo.[POR_GetEmployeeforLevelTwoIndex] @EmpeeId,@CompanyId,@UserType",
                    new Object[] {
                    new SqlParameter("@EmpeeId", EmployeeId),
                    new SqlParameter("@CompanyId", companyId),
                    new SqlParameter("@UserType", userType),
                    }).Select(x => new {

                        EmployeeIds = x.EmployeeId,
                        Name = x.EmployeeCode + "/" + x.FirstName + " " + x.LastName
                    }).Where(x => x.EmployeeIds != EmployeeId).ToList();

            return Json(new { dataList = list }, JsonRequestBehavior.AllowGet);
        }




        public List<Employee> GetLeaveEmployeesDropDown()
        {
            
            int employeeId = 0;
            int companyId = Convert.ToInt32(Session["CompanyId"]);
            if (Session["EmployeeId"] != null || Session["EmployeeId"] != "")
            {
                employeeId = Convert.ToInt32(Session["EmployeeId"]);
            }
            string userType = Session["UserTypeId"].ToString();

            var list = db.Database.SqlQuery<EmployeeDropdown>(
                   "exec dbo.[POR_GetEmployeeforLevelTwoIndex] @EmpeeId,@CompanyId,@UserType",
                    new Object[] {
                    new SqlParameter("@EmpeeId", employeeId),
                    new SqlParameter("@CompanyId", companyId),
                    new SqlParameter("@UserType", userType),
                    }).ToList();

            var empList = list.ToList().Select(x => new Employee
            {
                EmployeeId = x.EmployeeId,
                EmployeeCode = x.EmployeeCode,
                FirstName = x.FirstName,
                LastName = x.LastName
            }).ToList();

            return empList;
        }

        public List<Lev_LeaveType> GetLeaveTypesDropDown()
        {
            var leaveTypeList = db.Lev_LeaveType.ToList();
            return leaveTypeList;
        }

        public List<HalfDayShift> GetHalfDayShiftDropDown()
        {
            int companyId = Convert.ToInt32(Session["CompanyId"]);
            var shiftList = db.Time_ShiftSetup.Where(x => x.AppliedLeaveQty == 0.5M).Join(
                    db.Time_ShiftTypes.Where(x=>x.CompanyId == companyId),
                    ss => ss.ShiftId,
                    st => st.ShiftId,
                    (ss, st) => new { Time_ShiftSetup = ss, Time_ShiftTypes = st }).Select(x => new HalfDayShift
                    {
                        ShiftId = x.Time_ShiftTypes.ShiftId,
                        ShiftName = x.Time_ShiftTypes.ShiftDescription
                    }).ToList();
            return shiftList;
        }

        public ActionResult GetEmployees()
        {
            int employeeId = 0;
            if(Session["EmployeeId"]!=null || Session["EmployeeId"]!="")
            {
                employeeId = (int)Session["EmployeeId"];
            }
            var employees = db.TestEmployees.Where(x=>x.EmployeeID!= employeeId).OrderBy(a => a.Name).ToList();
            return Json(new { data = employees }, JsonRequestBehavior.AllowGet);
            
        }

        public List<Lev_LeaveReason> GetLeaveResonDropDown()
        {
            var leaveReasonList = db.Lev_LeaveReason.ToList();
            return leaveReasonList;
        }

        IEnumerable<TestEmployee> GetAllEmployee()
        {
            using (Dhigurah_DBEntities db = new Dhigurah_DBEntities())
            {
                return db.TestEmployees.ToList<TestEmployee>();
            }

        }

        public JsonResult SaveDataInDatabase(TestEmployee model)
        {
            var result = false;
            try
            {
                if (model.EmployeeID > 0)
                {
                    TestEmployee emp = db.TestEmployees.Where(x => x.EmployeeID == model.EmployeeID).SingleOrDefault();
                    emp.Name = model.Name;
                    emp.Position = model.Position;
                    emp.Office = model.Office;
                    emp.Salary = model.Salary;
                    db.SaveChanges();
                    result = true;
                }
                else
                {
                    TestEmployee emp = new TestEmployee();
                    emp.Name = model.Name;
                    emp.Position = model.Position;
                    emp.Office = model.Office;
                    emp.Salary = model.Salary;
                    db.TestEmployees.Add(emp);
                    db.SaveChanges();
                    result = true;
                }

                return Json(new { success = result,  message = "Submitted Successfully" }, JsonRequestBehavior.AllowGet);
            }
            catch(Exception ex)
            {
                return Json(new { success = result,  message = ex.Message }, JsonRequestBehavior.AllowGet);
            }

            
        }

        public JsonResult GetEmployeeById(int EmployeeId)
        {
            TestEmployee emp = db.TestEmployees.Where(x => x.EmployeeID == EmployeeId).SingleOrDefault();
            string value = JsonConvert.SerializeObject(emp, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
            return Json(value, JsonRequestBehavior.AllowGet);
        }

        public JsonResult DeleteEmployee(int EmployeeId)
        {
            var result = false;
            try
            {
                TestEmployee emp = db.TestEmployees.Where(x => x.EmployeeID == EmployeeId).SingleOrDefault();
                db.TestEmployees.Remove(emp);
                db.SaveChanges();
                result = true;
                List<GetMainMenues_Result> mainMenuList = GetMainMenues();
                return Json(new { success = result, message = "Delete Successfully" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                result = false;
                return Json(new { success = result, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public List<GetMainMenues_Result> GetMainMenues()
        {
            List<GetMainMenues_Result> mainMenuList = db.GetMainMenues().Select(x => new GetMainMenues_Result()
            {
                UserId = x.UserId,
                MainMenuId = x.MainMenuId,
                MenuName = x.MenuName
            }).ToList();
            return mainMenuList;
        }

        public List<GetSubMenues_Result> GetSubMenues()
        {
            List<GetSubMenues_Result> subMenuList = db.GetSubMenues().Select(x => new GetSubMenues_Result()
            {
                UserId = x.UserId,
                MainMenuId = x.MainMenuId,
                MenuName = x.MenuName,
                SubMenuId = x.SubMenuId,
                IconClass = x.IconClass
            }).ToList();
            return subMenuList;
        }

        public List<GetForms_Result> GetForms()
        {
            List<GetForms_Result> formList = db.GetForms().Select(x => new GetForms_Result()
            {
                UserId = x.UserId,
                MainMenuId = x.MainMenuId,
                SubMenuId = x.SubMenuId,
                FormId = x.FormId,
                Title = x.Title,
                PageName = x.PageName,
                ControllerName = x.ControllerName,
                VisibleIndex = x.VisibleIndex
            }).ToList();
            return formList;
        }

        public ActionResult LoadProfileImage(long EmployeeId)
        {
            var correctPath = "";
            var path = db.Employees.Where(x => x.EmployeeId == EmployeeId).Select(x => x.Image).FirstOrDefault();
            if (path == "" || path == null)
            {
                correctPath = ConfigurationManager.AppSettings["DefaultProfileImagePath"];
            }
            else
            {
                correctPath = ConfigurationManager.AppSettings["EmployeeImagePath"] + path;
            }

            return File(correctPath, "image/*");
        }

        public JsonResult GetNotifications(int EmployeeId)
        {
            var result = false;
            try
            {
                var notList = db.Por_Notification.Where(x => x.EmployeeId == EmployeeId && x.IsExpired == false).Join(
                        db.Por_NotificationType,
                        no => no.NotificationTypeId,
                        nt => nt.NotificationTypeId,
                        (no, nt) => new { Por_Notification = no, Por_NotificationType = nt }).ToList().Select(x => new
                        {
                            x.Por_Notification.NotificationId,
                            x.Por_Notification.Content,
                            x.Por_Notification.ControllerName,
                            x.Por_Notification.FunctionName,
                            x.Por_NotificationType.Color,
                            x.Por_NotificationType.Icon,
                            x.Por_NotificationType.NotificationType,
                            CreatedDate = x.Por_Notification.CreatedDate?.ToString("dd MMMM yyyy hh:mm tt", CultureInfo.InvariantCulture)
                        }).OrderByDescending(x => x.NotificationId).ToList();

                result = true;

                return Json(new { success = result, dataList = notList, count = notList.Count }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                result = false;
                return Json(new { success = result, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public JsonResult SelectNotification(long NotificationId)
        {
            var result = false;
            try
            {
                var notiList = db.Por_Notification.Where(x => x.NotificationId == NotificationId).FirstOrDefault();
                notiList.IsExpired = true;
                db.SaveChanges();

                result = true;

                return Json(new { success = result, controller = notiList.ControllerName, method = notiList.FunctionName }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                result = false;
                return Json(new { success = result, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}