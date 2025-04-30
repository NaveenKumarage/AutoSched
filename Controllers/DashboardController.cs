using CRUD_OperationByMeUsingJqueryAjaxMvc.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;



namespace CRUD_OperationByMeUsingJqueryAjaxMvc.Controllers
{
    public class DashboardController : Controller
    {
        // GET: Dashboard

        Dhigurah_DBEntities db = new Dhigurah_DBEntities();

        public ActionResult Index()
        {
            return View();
        }

        public JsonResult GetWeeklyAttendance(long EmployeeId)
        {
            //List<POR_GetWeeklyAttendance_Result> EmpAttList = db.POR_GetWeeklyAttendance().Where(x => x.EmployeeId == Convert.ToInt64(Session["EmployeeId"])).Select(x => new POR_GetWeeklyAttendance_Result()
            //{
            //    EmployeeId = x.EmployeeId,
            //    DateName = x.DateName,
            //    Day = x.Day,
            //    DayTitle = x.DayTitle,
            //    WorkingHrs = x.WorkingHrs,
            //    Month = x.Month
            //}).ToList();

            // for test
            string abc = "abc";

            DateTime fromDate = DateTime.Today;
            DateTime toDate = DateTime.Today.AddDays(-6);
            var defaultShiftHrs = db.Time_Employee.Where(x => x.EmployeeId == EmployeeId).Join(
                    db.Time_ShiftSetup,
                    te => te.DefaultShiftId,
                    ts => ts.ShiftId,
                    (te,ts) => new { Time_Employee = te , Time_ShiftSetup = ts}).Select(x => x.Time_ShiftSetup.ShiftHours).FirstOrDefault();

                    var EmpAttList = db.Time_EmployeeTxn
            .Where(x => x.EmployeeId == EmployeeId && x.Date >= toDate && x.Date <= fromDate)
            .Join(
                db.Time_Employee,
                tt => tt.EmployeeId,
                em => em.EmployeeId,
                (tt, em) => new { Time_EmployeeTxn = tt, Time_Employee = em }
            )
            .GroupJoin(
                db.Time_ShiftSetup,
                tt => tt.Time_EmployeeTxn.ShiftId,
                ts => ts.ShiftId,
                (tt, ts) => new { tt.Time_EmployeeTxn, tt.Time_Employee, Time_ShiftSetup = ts }
            )
            .ToList()
            .SelectMany(x => x.Time_ShiftSetup.DefaultIfEmpty(null),
                (x,y) => new
                {
                  
                    x.Time_EmployeeTxn.WorkingHrs,
                    DayName = x.Time_EmployeeTxn.Date.ToString("dddd", CultureInfo.InvariantCulture),
                    MonthName = x.Time_EmployeeTxn.Date.ToString("MMM", CultureInfo.InvariantCulture),
                    Day = x.Time_EmployeeTxn.Date.Day,
                    x.Time_EmployeeTxn.Date
                }
            )
            .OrderBy(x => x.Date)
            .ToList();


            /*var EmpAttList = db.Time_EmployeeTxn.Where(x => x.EmployeeId == EmployeeId && x.Date >= toDate && x.Date <= fromDate).Join(
                        db.Time_Employee,
                        tt => tt.EmployeeId,
                        em => em.EmployeeId,
                        (tt, em) => new { Time_EmployeeTxn = tt, Time_Employee = em }).GroupJoin(
                        db.Time_ShiftSetup,
                        tt => tt.Time_EmployeeTxn.ShiftId,
                        ts => ts.ShiftId,
                        (tt, ts) => new { tt.Time_EmployeeTxn, tt.Time_Employee, Time_ShiftSetup = ts }).ToList().SelectMany(x =>
                                  x.Time_ShiftSetup.DefaultIfEmpty(null),
                        (x, y) => new
                        {
                            WhProgress = (y == null || y.ShiftId == null ? (x.Time_EmployeeTxn.WorkingHrs/ defaultShiftHrs)*100 : (x.Time_EmployeeTxn.WorkingHrs / y.ShiftHours) * 100),
                            x.Time_EmployeeTxn.WorkingHrs,
                            DayName = x.Time_EmployeeTxn.Date.ToString("dddd", CultureInfo.InvariantCulture),
                            MonthName = x.Time_EmployeeTxn.Date.ToString("MMM", CultureInfo.InvariantCulture),
                            Day = x.Time_EmployeeTxn.Date.Day,
                            x.Time_EmployeeTxn.Date
                        }).OrderBy(x=> x.Date).ToList();*/


            return Json(EmpAttList, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetEmployees()
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
                    }).ToList();

            return Json(new { dataList = list }, JsonRequestBehavior.AllowGet);
        }


        public JsonResult GetHeadersData()
        {
            var result = false;
            decimal AnualLeaveBalance = 0;
            decimal CasualLeaveBalance = 0;
            decimal? AbsentDays = 0;
            int totalMinutes = 0;
            double totalLateHrsDouble = 0;
            string rightSide = "";
            decimal lieuLeaveBalance = 0;
            decimal MedicalWithoutMc = 0;
            decimal? Totalbalance = 0;

            try
            {
                long EmployeeId = Convert.ToInt64(Session["EmployeeId"]);

                Lev_LeaveEntitlement anuLe = db.Lev_LeaveEntitlement.Where(x => x.EmployeeId == EmployeeId && x.Year == DateTime.Now.Year && x.LeaveTypeId == 1031).SingleOrDefault();
                if (anuLe != null)
                {
                    AnualLeaveBalance = anuLe.Entitlement - anuLe.Utilized;
                    if (AnualLeaveBalance < 0)
                    {
                        AnualLeaveBalance = 0;
                    }
                }

                Lev_LeaveEntitlement casLe = db.Lev_LeaveEntitlement.Where(x => x.EmployeeId == EmployeeId && x.Year == DateTime.Now.Year && x.LeaveTypeId == 1058).SingleOrDefault();
                if (casLe != null)
                {
                    if ((double)casLe.Entitlement == 0.5)
                    {
                        CasualLeaveBalance = db.Lev_ProbationLeave.Where(x => x.Month <= DateTime.Today.Month && x.IsGet == false && x.EmployeeId == EmployeeId).Sum(x => x.Qty);
                    }
                    else
                    {
                        CasualLeaveBalance = casLe.Entitlement - casLe.Utilized;
                        if (CasualLeaveBalance < 0)
                        {
                            CasualLeaveBalance = 0;
                        }
                    }
                }
                Lev_LeaveEntitlement lieu = db.Lev_LeaveEntitlement.Where(x => x.EmployeeId == EmployeeId && x.Year == DateTime.Now.Year && x.LeaveTypeId == 1060).SingleOrDefault();
                if (lieu != null)
                {
                    if ((double)lieu.Entitlement == 0.5)
                    {
                        MedicalWithoutMc = db.Lev_ProbationLeave.Where(x => x.Month <= DateTime.Today.Month && x.IsGet == false && x.EmployeeId == EmployeeId).Sum(x => x.Qty);
                    }
                    else
                    {
                        MedicalWithoutMc = lieu.Entitlement - lieu.Utilized;
                        if (MedicalWithoutMc < 0)
                        {
                            MedicalWithoutMc = 0;
                        }
                    }
                }

                var payEmployee = db.Pay_EmplyoeePay.Where(x => x.EmployeeId == EmployeeId).Select(x => new { x.PayPeriodCategoryId }).FirstOrDefault();
                if(payEmployee != null)
                {
                    var payPeriod = db.Pay_PayPeriod.Where(x => x.PayPeriodCategoryId == payEmployee.PayPeriodCategoryId  && x.Complete==false).OrderBy(x => x.Year).ThenBy(x => x.Month).FirstOrDefault();
                    if(payPeriod != null)
                    {
                        AbsentDays = db.Database.SqlQuery<decimal?>(
                        "exec dbo.[TIME_GetEmployeeAbsentCount] @FromDate,@ToDate,@EmployeeId",
                        new Object[] {
                            new SqlParameter("@EmployeeId", EmployeeId),
                            new SqlParameter("@FromDate", payPeriod.BeginDate),
                            new SqlParameter("@ToDate", payPeriod.EndDate)
                        }
                    ).FirstOrDefault();

                        if(AbsentDays == null)
                        {
                            AbsentDays = 0;
                        }

                        totalMinutes = (int)db.Time_EmployeeTxn.Where(x => x.EmployeeId == EmployeeId && x.Date >= payPeriod.BeginDate && x.Date <= payPeriod.EndDate).ToList().Sum(s =>
                        {
                            if (s.Total > 0)
                            {
                                string input_decimal_number = s.Total.ToString();
                                var regex = new System.Text.RegularExpressions.Regex("(?<=[\\.])[0-9]+");
                                if (regex.IsMatch(input_decimal_number))
                                {
                                    int minutes = ((int)s.Total) * 60;
                                    string decimal_places = regex.Match(input_decimal_number).Value;
                                    minutes = minutes + Convert.ToInt32(decimal_places);
                                    return minutes;
                                }
                                else
                                {
                                    return 0;
                                }

                            }
                            else
                            {
                                return 0;
                            }

                        });

                        if (totalMinutes > 0)
                        {
                            rightSide = (totalMinutes % 60).ToString().Length == 1 ? "0" + (totalMinutes % 60).ToString() : (totalMinutes % 60).ToString();
                            totalLateHrsDouble = Convert.ToDouble(totalMinutes / 60 + "." + rightSide);
                        }
                    }
                }
                //var payEmployees = db.Pay_EmplyoeePay.Where(x => x.EmployeeId == EmployeeId).Select(x => new { x.PayPeriodCategoryId }).FirstOrDefault();
                //var payPeriodforMed = db.Pay_PayPeriod.Where(x => x.PayPeriodCategoryId == payEmployees.PayPeriodCategoryId && x.Posted == false && x.Complete == false)
                //.OrderBy(x => x.Year).ThenBy(x => x.Month).FirstOrDefault();

                //if (payPeriodforMed != null)
                // {

                Lev_LeaveEntitlement medi = db.Lev_LeaveEntitlement.Where(x => x.LeaveTypeId == 1059 && x.EmployeeId == EmployeeId).SingleOrDefault();
                if (medi != null)
                {
                    lieuLeaveBalance = medi.Entitlement - medi.Utilized;
                    if (lieuLeaveBalance < 0)
                    {
                        lieuLeaveBalance = 0;
                    }
                }
                //}
                if (payEmployee != null)
                {
                    var payPeriod = db.Pay_PayPeriod.Where(x => x.PayPeriodCategoryId == payEmployee.PayPeriodCategoryId && x.Complete == false).OrderBy(x => x.Year).ThenBy(x => x.Month).FirstOrDefault();
                    if (payPeriod != null)
                    {
                        Totalbalance = db.Database.SqlQuery<decimal?>(
                     "exec dbo.[TIME_GetEmployeeTotalAbsentCount] @EmployeeId",
                     new Object[] {
                            new SqlParameter("@EmployeeId", EmployeeId)
                         
                     }
                 ).FirstOrDefault();
                        if (Totalbalance == null)
                        {
                            Totalbalance = 0;
                        }
                    }
                 
                }
                result = true;

                return Json(new { success = result, anualBalance = AnualLeaveBalance.ToString(),casualBalance= CasualLeaveBalance.ToString(),
                    absentDays= MedicalWithoutMc.ToString(),lateHrs= totalLateHrsDouble.ToString(), Medicalbalance = lieuLeaveBalance.ToString(),Totalbalance = Totalbalance.ToString() }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = result, message = "0" }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult  GetEvents()
        {
            var eventList = GetEventsNew();

            var rows = eventList.ToArray();
            return Json(rows, JsonRequestBehavior.AllowGet);
        }

        private List<Events> GetEventsNew()
        {
            long employeeId = Convert.ToInt64(Session["EmployeeId"]);
            List<Events> eventList = new List<Events>();

            var leaveDetails = db.Lev_LeaveDetail.Join(
                    db.Lev_LeaveHeader.Where(x => x.EmployeeId == employeeId),
                    ld => ld.LeaveId,
                    lh => lh.LeaveId,
                    (ld, lh) => new { Lev_LeaveDetail = ld, Lev_LeaveHeader = lh }).Join(
                    db.Time_EmployeeTxn,
                    lh => new { Id = lh.Lev_LeaveHeader.EmployeeId , Date = lh.Lev_LeaveDetail.LeaveDate},
                    te => new { Id = te.EmployeeId , Date = te.Date},
                    (lh, te) => new { lh.Lev_LeaveHeader, lh.Lev_LeaveDetail, Time_EmployeeTxn = te }).Join(
                    db.Lev_LeaveType,
                    ld => ld.Lev_LeaveDetail.LeaveTypeId,
                    lt => lt.LeaveTypeId,
                    (ld, lt) => new { ld.Lev_LeaveHeader, ld.Lev_LeaveDetail, ld.Time_EmployeeTxn, Lev_LeaveType = lt }).Select(x => new
                    {
                        x.Lev_LeaveDetail.LeaveDate,
                        x.Lev_LeaveDetail.LeaveTypeId,
                        x.Lev_LeaveHeader.EmployeeId,
                        Session = (x.Lev_LeaveDetail.LeaveQty == 1 ? x.Lev_LeaveType.LeaveDescription + "|1.0" :
                         x.Lev_LeaveDetail.LeaveQty == (decimal)0.50 ? x.Lev_LeaveType.LeaveDescription + "|0.5" :
                         x.Lev_LeaveDetail.LeaveQty == (decimal)0.25 ? x.Lev_LeaveType.LeaveDescription + "|0.25" : ""),
                         x.Lev_LeaveHeader.Status
                    }).ToList();

            //List<POR_GetLeaveDetails_Result2> leaveDetails = db.POR_GetLeaveDetails().Where(x => x.EmployeeId == Convert.ToInt64(Session["EmployeeId"])).Select(x => new POR_GetLeaveDetails_Result2()
            //{
            //    EmployeeId=x.EmployeeId,
            //    LeaveDate=x.LeaveDate,
            //    LeaveTypeId=x.LeaveTypeId,
            //    Session=x.Session
            //}).ToList();

            foreach(var leaves in leaveDetails)
            {
                Events newEvent = new Events
                {
                    id = leaves.LeaveTypeId.ToString(),
                    title = leaves.Session.ToString(),
                    start = leaves.LeaveDate.ToString("MM/dd/yyyy"),
                    end = leaves.LeaveDate.ToString("MM/dd/yyyy"),
                    allDay = true,
                    status = leaves.Status
                };


                eventList.Add(newEvent);
            }

            return eventList;
        }

        [HttpPost]
        public ActionResult GetCalenderDayTitle(string StartDate,int CompanyId)
        {
            DateTime startDate = Convert.ToDateTime(StartDate);
            DateTime endDate = startDate.AddDays(1);

            var detailList = db.Time_Calender.Where(x => x.CompanyId == CompanyId && x.CalendarDate == startDate).Join(
                db.Time_DayTypes,
                tc => tc.DayTypeId,
                td => td.DayTypeId,
                (tc, td) => new { Time_Calender = tc, Time_DayTypes = td }).ToList().Select(s => new
                {
                    Title = s.Time_DayTypes.DayType,
                    Color = s.Time_DayTypes.BackGroundColour
                }).ToList();

            return Json(new { dataList = detailList }, JsonRequestBehavior.AllowGet);
        }
    }
}