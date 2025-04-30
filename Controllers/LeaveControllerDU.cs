using CRUD_OperationByMeUsingJqueryAjaxMvc.Models;
using CRUD_OperationByMeUsingJqueryAjaxMvc.Models.LeaveModel;
using CRUD_OperationByMeUsingJqueryAjaxMvc.Email;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using CRUD_OperationByMeUsingJqueryAjaxMvc.Models.RecruitmentModel;
using System.Dynamic;
using CRUD_OperationByMeUsingJqueryAjaxMvc.Models.Shared;
using System.Data.SqlClient;
using System.Runtime.Remoting.Lifetime;
using DevExpress.PivotGrid.OLAP.AdoWrappers;

namespace CRUD_OperationByMeUsingJqueryAjaxMvc.Controllers
{
    public class LeaveController : Controller
    {

        Dhigurah_DBEntities db = new Dhigurah_DBEntities();
        ConvertDateToAll dateConvert = new ConvertDateToAll();
        UserSecurityController uscObj = new UserSecurityController();
        string leaveMsg = "";
        // GET: Leave


      
        public ActionResult Index()
        {
            return View();
        }

        private bool HasPendingCoveringRequests(long employeeId, DateTime fromDate, DateTime toDate)
        {
            var pendingCoveringRequests = db.Lev_LeaveHeader
                .Where(lh => lh.CoveringPerson == employeeId && lh.CoveringPersonStatus == "Pending")
                .ToList();

            foreach (var request in pendingCoveringRequests)
            {
                if (request.FromDate <= toDate && request.Todate >= fromDate)
                {
                    return true; // There are pending covering requests that overlap with the selected leave dates
                }
            }

            return false; // No overlapping covering requests
        }

        [HttpPost]
        public JsonResult SaveLeaveDetails(SaveLeaveDetails leaveModel)
        {
            var result = false;

            try
            {
                DateTime fromDate = dateConvert.GetDateToAll(leaveModel.FromDate.ToString());
                DateTime toDate = dateConvert.GetDateToAll(leaveModel.ToDate.ToString());
                int CompanyId = (int)db.Employees.Where(x => x.EmployeeId == leaveModel.EmployeeId).Select(x => x.CompanyID).SingleOrDefault();
                DateTime dayOpen = DateTime.Parse(DateTime.Now.ToShortDateString() + " 05:00");
                string leaveTypeCode = db.Lev_LeaveType.Where(x => x.LeaveTypeId == leaveModel.LeaveTypeId).Select(x => x.LeaveCode).SingleOrDefault();

                if (CheckLeave(fromDate, toDate, leaveModel.TotalLeaveQty, CompanyId, leaveModel.EmployeeId, leaveModel.LeaveTypeId, leaveModel.IsHalfDay))
                {

                    if (HasPendingCoveringRequests((long)leaveModel.EmployeeId, fromDate, toDate))
                    {
                        leaveMsg = "You have pending covering requests for the selected leave dates. You cannot apply for leave on those days.";
                        return Json(new { success = result, message = leaveMsg }, JsonRequestBehavior.AllowGet);
                    }

                    //Check Payperiod for apply leaves
                     bool isLeaveTypeExpired = db.Lev_LeaveType.Where(x => x.LeaveTypeId == leaveModel.LeaveTypeId).Select(x => x.IsExpirered).FirstOrDefault();

                    if (isLeaveTypeExpired)
                    {
                        leaveMsg = "Pay Period is closed.";
                        return Json(new { success = result, message = leaveMsg }, JsonRequestBehavior.AllowGet);
                    }


                    var levWF = db.Com_CommonApprovalWorkFlow.Join(
                        db.Com_CommonAssignData,
                        cap => cap.ApprovalWorkFlowId,
                        cas => cas.ApprovalWorkFlowId,
                        (cap, cas) => new { Com_CommonApprovalWorkFlow = cap, Com_CommonAssignData = cas }).Join(
                        db.ApprovalTypes,
                        cas => cas.Com_CommonAssignData.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cas, at) => new { cas.Com_CommonApprovalWorkFlow, cas.Com_CommonAssignData, ApprovalType = at }).Where(
                        x => x.ApprovalType.ApprovalTypeCode == "LA" && x.Com_CommonAssignData.EmployeeId == leaveModel.EmployeeId).Select(v => new
                        {
                            v.Com_CommonApprovalWorkFlow.ApprovePerson1,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson2,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson3
                        }).ToList();

                    
                    //check payperiod
                    /*if (ValidPayroll.IsPostedPayperiod(leaveModel.EmployeeId, fromDate))
                    {
                        leaveMsg = "Cannot apply leaves after pay period posted.";
                        return Json(new { success = result, message = leaveMsg }, JsonRequestBehavior.AllowGet);
                    }*/

                    if (levWF.Count <= 0 && leaveModel.IsWorkflow)
                    {
                        leaveMsg = "Need to assign for a leave work flow.";
                        return Json(new { success = result, message = leaveMsg }, JsonRequestBehavior.AllowGet);
                    }
                    else
                    {
                        if(leaveTypeCode == "CASUAL")
                        {
                            bool isCheckCasualContinuos = CheckCasualContinuous(leaveModel.EmployeeId, fromDate, leaveModel.TotalLeaveQty, leaveModel.LeaveTypeId);
                            if (!isCheckCasualContinuos)
                            {
                                leaveMsg = "Casual leave days should not exceed 2 days.";
                                return Json(new { success = result, message = leaveMsg }, JsonRequestBehavior.AllowGet);
                            }
                        }

                        Lev_LeaveHeader leaveHeader = new Lev_LeaveHeader();
                        leaveHeader.EmployeeId = leaveModel.EmployeeId;
                        //leaveHeader.CoveringPerson = leaveModel.CoveringPerson == 0 ? null : leaveModel.CoveringPerson;
                        leaveHeader.LeaveNo = GetLastLeaveNo("LeaveApplication", CompanyId);
                        leaveHeader.FromDate = fromDate;
                        leaveHeader.Todate = toDate;
                        leaveHeader.LeaveTypeId = leaveModel.LeaveTypeId;
                        leaveHeader.TotalLeaveQty = leaveModel.TotalLeaveQty;
                        leaveHeader.Status = "Pending";
                        leaveHeader.LeaveReason = leaveModel.Reason;
                        leaveHeader.ISWorkFolw = leaveModel.IsWorkflow;
                        leaveHeader.IsApprovedLeave = true;
                        leaveHeader.CreatedDate = DateTime.Now;
                        leaveHeader.CreatedUser = leaveModel.CreatedUser;

                        if (leaveModel.IsWorkflow)
                        {
                            if (leaveModel.CoveringPerson == 0 || leaveModel.CoveringPerson == null)
                            {
                                leaveHeader.ApprovePerson1Status = "Pending";
                                leaveHeader.ApprovePerson1 = levWF.Select(x => x.ApprovePerson1).FirstOrDefault();
                                /*leaveHeader.ApprovePerson2Status = "Pending";
                                leaveHeader.ApprovePerson2 = levWF.Select(x => x.ApprovePerson2).FirstOrDefault();*/
                            }
                            //leaveHeader.CoveringPersonStatus = "Pending";
                            leaveHeader.Status = "Pending";
                            leaveHeader.ApprovePerson1Status = "Pending";
                            leaveHeader.ApprovePerson1 = levWF.Select(x => x.ApprovePerson1).FirstOrDefault();
                            /*leaveHeader.ApprovePerson2Status = "Pending";
                            leaveHeader.ApprovePerson2 = levWF.Select(x => x.ApprovePerson2).FirstOrDefault();*/
                        }
                        else
                        {
                            leaveHeader.Status = "Approved";
                        }

                        db.Lev_LeaveHeader.Add(leaveHeader);
                        db.SaveChanges();

                        long leaveId = leaveHeader.LeaveId;
                        var employeeShifts = db.Time_EmployeeShiftShedule.Where(x => x.EmployeeId == leaveModel.EmployeeId && x.ShiftDate >= fromDate && x.ShiftDate <= toDate).Join(
                            db.Time_ShiftSetup,
                            es => es.ShiftId,
                            ss => ss.ShiftId,
                            (es, ss) => new { Time_EmployeeShiftShedule = es, Time_ShiftSetup = ss }).Select(x => new
                            {
                                x.Time_EmployeeShiftShedule.ShiftDate,
                                x.Time_ShiftSetup.AppliedLeaveQty
                            }).ToList();
                        var calendarDetails = db.Time_Calender.Where(x => x.CalendarDate >= fromDate && x.CalendarDate <= toDate && x.CompanyId == CompanyId).ToList();
                        foreach (var dates in calendarDetails)
                        {
                            double leaveCount = 0;
                            string session = "";
                            decimal shiftLeaveQty = employeeShifts.Where(x => x.ShiftDate == dates.CalendarDate).Select(x => x.AppliedLeaveQty).FirstOrDefault();
                            var shiftDates = db.Time_EmployeeShiftShedule.Where(x => x.ShiftDate == dates.CalendarDate && x.EmployeeId == leaveModel.EmployeeId).SingleOrDefault();
                            if (shiftDates != null)
                            {
                                if (leaveTypeCode == "SHORT")
                                {
                                    session = "ShortLeave - " + leaveModel.Session;
                                    leaveCount = 0.50;
                                }
                                else
                                {
                                    if (leaveModel.IsHalfDay)
                                    {
                                        session = "HalfDay - " + leaveModel.Session;
                                        leaveCount = 0.5;
                                    }
                                    else
                                    {
                                        var timeTxn = db.Time_EmployeeTxn.Where(x => x.EmployeeId == leaveModel.EmployeeId && x.Date == dates.CalendarDate).Select(x => new
                                        {
                                            x.IsDayOff,
                                            x.DayOffLeaveQty
                                        }).FirstOrDefault();
                                        if ((timeTxn.IsDayOff == true && timeTxn.DayOffLeaveQty == 0.5M) || (timeTxn.IsDayOff == false && shiftLeaveQty == 0.5M))
                                        {
                                            if (shiftLeaveQty ==2)
                                            {
                                                leaveCount = 1;
                                                session = "HalfDay";
                                            }
                                            else
                                            {
                                                leaveCount = 0.5;
                                                session = "HalfDay";
                                            }
                                           
                                        }
                                        else if(shiftLeaveQty !=2)
                                        {
                                            leaveCount = 1;
                                            session = "FullDay";
                                        }
                                        else
                                        {
                                            leaveCount = 2;
                                            session = "FullDay";
                                        }
                                    }
                                }
                            }
                            else if (leaveTypeCode == "DAYOFFFULL" || leaveTypeCode == "DAYOFFHALF")
                            {
                                if (leaveTypeCode == "DAYOFFFULL")
                                {
                                    leaveCount = 1;
                                    session = "FullDay";
                                }
                                else
                                {
                                    leaveCount = 0.5;
                                    session = "HalfDay";
                                }
                            }
                            else
                            {
                                if (leaveTypeCode == "SHORT")
                                {
                                    session = "ShortLeave - " + leaveModel.Session;
                                    leaveCount = 0.50;
                                }
                                else
                                {
                                    if (leaveModel.IsHalfDay)
                                    {
                                        session = "HalfDay - " + leaveModel.Session;
                                        leaveCount = 0.5;
                                    }
                                    else
                                    {
                                        var timeTxn = db.Time_EmployeeTxn.Where(x => x.EmployeeId == leaveModel.EmployeeId && x.Date == dates.CalendarDate).Select(x => new
                                        {
                                            x.IsDayOff,
                                            x.DayOffLeaveQty
                                        }).FirstOrDefault();
                                        if ((timeTxn.IsDayOff == true && timeTxn.DayOffLeaveQty == 0.5M) || (timeTxn.IsDayOff == false && shiftLeaveQty == 0.5M))
                                        {
                                            if (shiftLeaveQty == 2)
                                            {
                                                leaveCount = 1;
                                                session = "HalfDay";
                                            }
                                            else
                                            {
                                                leaveCount = 0.5;
                                                session = "HalfDay";
                                            }

                                        }
                                        else if (shiftLeaveQty != 2)
                                        {
                                            leaveCount = 1;
                                            session = "FullDay";
                                        }
                                        else
                                        {
                                            leaveCount = 2;
                                            session = "FullDay";
                                        }
                                    }
                                }
                            }

                            if (leaveCount > 0 && leaveId != null && leaveId != 0)
                            {
                                Lev_LeaveDetail ld = new Lev_LeaveDetail();
                                ld.LeaveId = leaveId;
                                ld.LeaveTypeId = leaveModel.LeaveTypeId;
                                ld.LeaveDate = dates.CalendarDate;
                                ld.LeaveQty = (decimal)leaveCount;
                                ld.Session = session;
                                ld.DayTypeId = dates.DayTypeId;
                                ld.ShiftId = 1;
                                ld.DateName = dates.CalendarDate.ToString("dddd");
                                ld.IsActive = true;
                                ld.DeductedOT = leaveModel.DeductOT;
                                if (leaveModel.IsWorkflow)
                                {
                                    ld.CoveringPersonStatus = "Pending";
                                    ld.LeaveStatus = "Pending";
                                }
                                else
                                {
                                    ld.LeaveStatus = "Approved";
                                }
                                db.Lev_LeaveDetail.Add(ld);
                                db.SaveChanges();

                                if (leaveModel.IsWorkflow)
                                {
                                    SetLeaveCalculations(leaveModel.EmployeeId, leaveModel.LeaveTypeId, dates.CalendarDate, leaveCount, leaveHeader.LeaveNo, leaveHeader.CreatedUser, 0);
                                }

                                leaveMsg = "Submitted Successfully.";
                            }

                        }

                        if (leaveModel.IsWorkflow)
                        {
                            if (leaveModel.CoveringPerson == 0)
                            {
                                InsertLeaveNotificationForApprovePerson(leaveModel.EmployeeId, (long)levWF.Select(x => x.ApprovePerson1).FirstOrDefault(), fromDate, toDate);
                                //SendEmailToApprovePerson(leaveId, (long)levWF.Select(x => x.ApprovePerson1).FirstOrDefault());

                            }
                            else
                            {
                                //SendEmailToApprovePerson(leaveId, (long)levWF.Select(x => x.ApprovePerson1).FirstOrDefault());
                                InsertLeaveNotificationForApprovePerson(leaveModel.EmployeeId, (long)levWF.Select(x => x.ApprovePerson1).FirstOrDefault(), fromDate, toDate);
                                //InsertLeaveNotificationForCovering((long)leaveModel.CoveringPerson, leaveModel.EmployeeId, true);
                                //SendEmailToCoveringPerson(leaveId, (long)leaveModel.CoveringPerson);

                            }

                        }
                    }

                    result = true;
                }

                return Json(new { success = result, message = leaveMsg }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = result, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private bool CheckCasualContinuous(long EmployeeId, DateTime Fromdate, decimal LeaveQty, int LeaveTypeId)
        {
            decimal totalLeaveQty = 0;
            var shiftList = db.Time_EmployeeShiftShedule.Where(x => x.EmployeeId == EmployeeId && x.ShiftDate < Fromdate).OrderByDescending(x => x.ShiftDate).Take(4).ToList();
            foreach(var shift in shiftList)
            {
                var shiftDayLeaveQty = db.Lev_LeaveDetail.Join(
                                db.Lev_LeaveHeader,
                                ld => ld.LeaveId,
                                lh => lh.LeaveId,
                                (ld, lh) => new { Lev_LeaveDetail = ld, Lev_LeaveHeader = lh }).Where(x =>
                                x.Lev_LeaveHeader.EmployeeId == EmployeeId && x.Lev_LeaveHeader.Status != "Rejected" && x.Lev_LeaveHeader.LeaveTypeId == LeaveTypeId
                                && x.Lev_LeaveDetail.LeaveDate == shift.ShiftDate).
                                ToList().Sum(x => x.Lev_LeaveDetail.LeaveQty);

                if (shiftDayLeaveQty > 0)
                {
                    totalLeaveQty = totalLeaveQty + shiftDayLeaveQty;
                    if(totalLeaveQty + LeaveQty > 2)
                    {
                        return false;
                    }
                }
                else
                {
                    return true;
                }

            }
            return true;
        }

        private bool CheckLeave(DateTime FromDate, DateTime ToDate, decimal totalLeaveQty, int CompanyId, long EmployeeId, int LeaveTypeId, bool IsHalfDay)
        {
            var employeeShifts = db.Time_EmployeeShiftShedule.Where(x => x.EmployeeId == EmployeeId && x.ShiftDate >= FromDate && x.ShiftDate <= ToDate).Join(
                        db.Time_ShiftSetup,
                        es => es.ShiftId,
                        ss => ss.ShiftId,
                        (es, ss) => new { Time_EmployeeShiftShedule = es, Time_ShiftSetup = ss }).Select(x => new {
                            x.Time_EmployeeShiftShedule.ShiftDate,
                            x.Time_ShiftSetup.AppliedLeaveQty
                        }).ToList();
            string leaveTypeCode = db.Lev_LeaveType.Where(x => x.LeaveTypeId == LeaveTypeId).Select(x => x.LeaveCode).SingleOrDefault();
            var calendarDetails = db.Time_Calender.Where(x => x.CalendarDate >= FromDate && x.CalendarDate <= ToDate && x.CompanyId == CompanyId).ToList();
            decimal prvsPendingLeaveQty = 0;

            if (leaveTypeCode == "SHORT")
            {
                decimal prvsShortLeaveQty = db.Lev_LeaveDetail.Join(
                                db.Lev_LeaveHeader,
                                ld => ld.LeaveId,
                                lh => lh.LeaveId,
                                (ld, lh) => new { Lev_LeaveDetail = ld, Lev_LeaveHeader = lh }).Join(
                                db.Lev_LeaveType,
                                ld => ld.Lev_LeaveDetail.LeaveTypeId,
                                lt => lt.LeaveTypeId,
                                (ld, lt) => new { ld.Lev_LeaveDetail, ld.Lev_LeaveHeader, Lev_LeaveType = lt }
                                ).Where(x =>
                                x.Lev_LeaveHeader.EmployeeId == EmployeeId && x.Lev_LeaveHeader.Status != "Rejected" && x.Lev_LeaveType.LeaveCode == "SHORT" && x.Lev_LeaveDetail.LeaveDate.Month == FromDate.Month).
                                ToList().Sum(x => x.Lev_LeaveDetail.LeaveQty);

                if ((double)(prvsShortLeaveQty + totalLeaveQty) > 2.0)
                {
                    if ((double)prvsShortLeaveQty == 2.0)
                    {
                        leaveMsg = "No remaining short leaves. Please check with previous applied leaves.";
                        return false;
                    }
                }
            }
            else if (leaveTypeCode == "ANNUAL" || leaveTypeCode == "CASUAL" || leaveTypeCode == "MATERNITY" ||
                leaveTypeCode == "LIEU" || leaveTypeCode == "DAYOFFFULL" || leaveTypeCode == "DAYOFFHALF" || leaveTypeCode == "COVERING")
            {
                var list = db.Database.SqlQuery<GetRemainingLeave>(
                   "exec dbo.[Lev_GetRemainingAnnualLeave] @EmployeeId,@Year,@Month,@LeaveTypeId,@ToDate",
                    new Object[] {
                    new SqlParameter("@EmployeeId", EmployeeId),
                    new SqlParameter("@Year", FromDate.Year),
                    new SqlParameter("@Month", FromDate.Month),
                    new SqlParameter("@LeaveTypeId", LeaveTypeId),
                    new SqlParameter("@ToDate", ToDate)}
                    ).FirstOrDefault();

                if (leaveTypeCode == "ANNUAL" || leaveTypeCode == "CASUAL" || leaveTypeCode == "MATERNITY")
                {
                    prvsPendingLeaveQty = db.Lev_LeaveDetail.Join(
                                db.Lev_LeaveHeader,
                                ld => ld.LeaveId,
                                lh => lh.LeaveId,
                                (ld, lh) => new { Lev_LeaveDetail = ld, Lev_LeaveHeader = lh }).Where(x =>
                                x.Lev_LeaveHeader.EmployeeId == EmployeeId && x.Lev_LeaveHeader.Status == "Pending" && x.Lev_LeaveHeader.LeaveTypeId == LeaveTypeId
                                && x.Lev_LeaveDetail.LeaveDate.Year == FromDate.Year).
                                ToList().Sum(x => x.Lev_LeaveDetail.LeaveQty);
                }
                else if (leaveTypeCode == "DAYOFFFULL" || leaveTypeCode == "DAYOFFHALF")
                {
                    prvsPendingLeaveQty = db.Lev_LeaveDetail.Join(
                                db.Lev_LeaveHeader,
                                ld => ld.LeaveId,
                                lh => lh.LeaveId,
                                (ld, lh) => new { Lev_LeaveDetail = ld, Lev_LeaveHeader = lh }).Join(
                                db.Lev_LeaveType,
                                ld => ld.Lev_LeaveDetail.LeaveTypeId,
                                lt => lt.LeaveTypeId,
                                (ld, lt) => new { ld.Lev_LeaveDetail, ld.Lev_LeaveHeader, Lev_LeaveType = lt }
                                ).Where(x => x.Lev_LeaveHeader.EmployeeId == EmployeeId && x.Lev_LeaveHeader.Status != "Pending" &&
                                (x.Lev_LeaveType.LeaveCode == "DAYOFFFULL" || x.Lev_LeaveType.LeaveCode == "DAYOFFFULL") &&
                                x.Lev_LeaveDetail.LeaveDate.Month == FromDate.Month && x.Lev_LeaveDetail.LeaveDate.Year == FromDate.Year).
                                ToList().Sum(x => x.Lev_LeaveDetail.LeaveQty);
                }
                else if (leaveTypeCode == "LIEU" || leaveTypeCode == "COVERING")
                {
                    prvsPendingLeaveQty = db.Lev_LeaveDetail.Join(
                                db.Lev_LeaveHeader,
                                ld => ld.LeaveId,
                                lh => lh.LeaveId,
                                (ld, lh) => new { Lev_LeaveDetail = ld, Lev_LeaveHeader = lh }).Where(x =>
                                x.Lev_LeaveHeader.EmployeeId == EmployeeId && x.Lev_LeaveHeader.Status == "Pending" && x.Lev_LeaveHeader.LeaveTypeId == LeaveTypeId &&
                                x.Lev_LeaveDetail.LeaveDate < ToDate && x.Lev_LeaveDetail.LeaveDate.Year == ToDate.Year).
                                ToList().Sum(x => x.Lev_LeaveDetail.LeaveQty);
                }

                if ((prvsPendingLeaveQty + totalLeaveQty) > list.RemainingCount)
                {
                    leaveMsg = "No remaining leaves. Please check with previous applied leaves.";
                    return false;
                }
            }

            foreach (var dates in calendarDetails)
            {
                double leaveQty = 0;
                DateTime date = dates.CalendarDate;
                int dayTypeId = dates.DayTypeId;
                var shiftDates = employeeShifts.Where(x => x.ShiftDate == date).FirstOrDefault();
                decimal shiftLeaveQty = employeeShifts.Where(x => x.ShiftDate == date).Select(x => x.AppliedLeaveQty).FirstOrDefault();
                decimal prvsLeaveQty = db.Lev_LeaveDetail.Join(
                                db.Lev_LeaveHeader,
                                ld => ld.LeaveId,
                                lh => lh.LeaveId,
                                (ld, lh) => new { Lev_LeaveDetail = ld, Lev_LeaveHeader = lh }).Where(x =>
                                x.Lev_LeaveHeader.EmployeeId == EmployeeId && x.Lev_LeaveDetail.LeaveDate == date && x.Lev_LeaveHeader.Status != "Rejected").ToList().Sum(x => x.Lev_LeaveDetail.LeaveQty);

                if (shiftDates != null)
                {
                    if (leaveTypeCode == "SHORT")
                    {
                        leaveQty = 0.25;
                    }
                    else
                    {
                        if (IsHalfDay)
                        {
                            leaveQty = 0.5;
                        }
                        else
                        {
                            var timeTxn = db.Time_EmployeeTxn.Where(x => x.EmployeeId == EmployeeId && x.Date == date).Select(x => new
                            {
                                x.IsDayOff,
                                x.DayOffLeaveQty
                            }).FirstOrDefault();
                            if ((timeTxn.IsDayOff == true && timeTxn.DayOffLeaveQty == 0.5M) || (timeTxn.IsDayOff == false && shiftLeaveQty == 0.5M))
                            {
                                leaveQty = 0.5;
                            }
                            else
                            {
                                leaveQty = 1;
                            }
                        }
                    }
                    if (((double)prvsLeaveQty) + leaveQty > 1)
                    {
                        leaveMsg = "Leave limit exceed for a day on " + date.ToString("MM/dd/yyyy") + " date.";
                        return false;
                    }
                    if (leaveTypeCode != "SHORT" && leaveQty == 1)
                    {
                        decimal? workingHrs = db.Time_EmployeeTxn.Where(x => x.EmployeeId == EmployeeId && x.Date == date).Select(x => x.WorkingHrs).SingleOrDefault();

                        if (workingHrs == null)
                            workingHrs = 0;

                        if (workingHrs >= 9)
                        {
                            leaveMsg = "Can't apply full day leave for worked " + date.ToString("MM/dd/yyyy") + " day.";
                            return false;
                        }
                    }
                }
                else
                {
                
                    if (leaveTypeCode == "DAYOFFFULL" || leaveTypeCode == "DAYOFFHALF")
                    {
                        if (leaveTypeCode == "DAYOFFHALF")
                        {
                            leaveQty = 0.5;
                        }
                        else
                        {
                            leaveQty = 1;
                        }
                        if (((double)prvsLeaveQty) + leaveQty > 1)
                        {
                            leaveMsg = "Leave limit exceed for a day on " + date.ToString("MM/dd/yyyy") + " date.";
                            return false;
                        }
                    }
                    else
                    {
                        if (IsHalfDay)
                        {
                            leaveQty = 0.5;
                        }
                        else
                        {
                            var timeTxn = db.Time_EmployeeTxn.Where(x => x.EmployeeId == EmployeeId && x.Date == date).Select(x => new
                            {
                                x.IsDayOff,
                                x.DayOffLeaveQty
                            }).FirstOrDefault();
                            if ((timeTxn.IsDayOff == true && timeTxn.DayOffLeaveQty == 0.5M) || (timeTxn.IsDayOff == false && shiftLeaveQty == 0.5M))
                            {
                                leaveQty = 0.5;
                            }
                            else
                            {
                                leaveQty = 1;
                            }
                        }
                        if (((double)prvsLeaveQty) + leaveQty > 1)
                        {
                            leaveMsg = "Leave limit exceed for a day on " + date.ToString("MM/dd/yyyy") + " date.";
                            return false;
                        }
                    }
                    if (dayTypeId != 7 && dayTypeId != 8 && dayTypeId != 9 && dayTypeId != 2014 && dayTypeId != 2015 && leaveTypeCode != "DAYOFFFULL" && leaveTypeCode != "DAYOFFHALF")
                    {
                        leaveMsg = "No shift Assign to " + date.ToString("MM/dd/yyyy") + ".";
                        return false;
                    }
                }
            }
            return true;
        }

        private string GetLastLeaveNo(string SetupName, int CompanyId)
        {
            string leaveNo = "";
            var setupDetails = db.Setups.Where(x => x.CompanyId == CompanyId && x.SetUpName == SetupName).SingleOrDefault();
            if (setupDetails != null)
            {
                leaveNo = setupDetails.Prefix + "" + (setupDetails.LastId + 1).ToString().PadLeft((setupDetails.LeadingZeros), '0');
                setupDetails.LastId = setupDetails.LastId + 1;
                setupDetails.PreviousNo = leaveNo;
                db.SaveChanges();
            }
            return leaveNo;
        }

        private string GetCurrentLeaveNo(string SetupName, int CompanyId)
        {
            string leaveNo = "";
            var setupDetails = db.Setups.Where(x => x.CompanyId == CompanyId && x.SetUpName == SetupName).SingleOrDefault();
            if (setupDetails != null)
            {
                leaveNo = setupDetails.Prefix + "" + (setupDetails.LastId).ToString().PadLeft((setupDetails.LeadingZeros), '0');
            }
            return leaveNo;
        }

        private void SetLeaveCalculations(long EmployeeId, int LeaveTypeId, DateTime LeaveDate, double LeaveQty, string LeaveNo, string CreatedUser, decimal? DeductOT)
        {
            try
            {
                var timeTxn = db.Time_EmployeeTxn.Where(x => x.EmployeeId == EmployeeId && x.Date == LeaveDate).FirstOrDefault();
                string leaveTypeCode = db.Lev_LeaveType.Where(x => x.LeaveTypeId == LeaveTypeId).Select(x => x.LeaveCode).SingleOrDefault();
                string leaveTypeFromTxn = db.Time_EmployeeTxn.Where(x=>x.EmployeeId == EmployeeId && x.Date == LeaveDate).Select(x=>x.LeaveType).SingleOrDefault();


                //Apply leave for nopay

                if ((leaveTypeFromTxn == "NOPAY" || leaveTypeFromTxn == "PAYCUTNOPAY" || leaveTypeFromTxn == "NOMALLAtePAYCUT") && leaveTypeCode == "ANNUAL") 
                {
                    timeTxn.LeaveNo = LeaveNo;
                    timeTxn.LeaveType = "Annual";
                    timeTxn.LeaveQty = 0.5M;

                    timeTxn.LeaveNo2 = null;
                    timeTxn.LeaveType2 = null;
                    timeTxn.LeaveQty2 = null;

                    db.SaveChanges();

                    var levEnt = db.Lev_LeaveEntitlement.Where(le => le.EmployeeId == EmployeeId && le.Year == LeaveDate.Year && le.LeaveTypeId == LeaveTypeId).SingleOrDefault();
                   /* if (levEnt != null)
                    {
                        levEnt.Utilized = levEnt.Utilized + (decimal)LeaveQty;
                        db.SaveChanges();
                    }*/


                }

                else if (leaveTypeCode == "SHORT")
                {
                    var shortLeave = db.Lev_ShortLeave.Where(x => x.EmployeeId == EmployeeId && x.Month == LeaveDate.Month && x.Year == LeaveDate.Year).FirstOrDefault();
                    if (shortLeave != null)
                    {
                        shortLeave.ShortLeaveMinutes = shortLeave.ShortLeaveMinutes - 60.0M;
                        db.SaveChanges();
                    }
                }
                else if (leaveTypeCode == "DUTY")
                {

                    int shiftId = db.Time_EmployeeShiftShedule.Where(x => x.EmployeeId == EmployeeId && x.ShiftDate == LeaveDate).Select(x => x.ShiftId).SingleOrDefault();
                    var shiftDetails = db.Time_ShiftSetup.Where(x => x.ShiftId == shiftId).SingleOrDefault();
                    if (shiftDetails != null)
                    {
                        var timeTxnDetails = db.Time_EmployeeTxn.Where(x => x.EmployeeId == EmployeeId && x.Date == LeaveDate).SingleOrDefault();
                        if (timeTxnDetails != null)
                        {
                            timeTxnDetails.FirstInTime = timeTxnDetails.FirstInTime;
                            timeTxnDetails.FirstOutTime = timeTxnDetails.FirstOutTime;
                            timeTxnDetails.ApproveLateHrs = 0;
                            timeTxnDetails.ApproveEarlyHrs = 0;
                            db.SaveChanges();
                        }
                    }
                }
                else if (leaveTypeCode == "LIEU")
                {
                    if (LeaveQty == 0.5)
                    {
                        var lieu = db.Lev_LeaveLeaveDetails.Where(x => x.EmployeeId == EmployeeId && x.LeaveDate <= LeaveDate && x.Year == LeaveDate.Year && x.Qty == 0.5M && x.IsActive == true).OrderBy(x => x.LeaveDate).FirstOrDefault();
                        if (lieu != null)
                        {
                            lieu.IsActive = false;
                            db.SaveChanges();
                        }
                        else
                        {
                            var lieu2 = db.Lev_LeaveLeaveDetails.Where(x => x.EmployeeId == EmployeeId && x.LeaveDate <= LeaveDate && x.Year == LeaveDate.Year && x.Qty == 1 && x.IsActive == true).OrderBy(x => x.LeaveDate).FirstOrDefault();
                            if (lieu2 != null)
                            {
                                lieu2.Qty = 0.5M;
                                db.SaveChanges();
                            }
                        }
                    }
                    else
                    {
                        var lieu = db.Lev_LeaveLeaveDetails.Where(x => x.EmployeeId == EmployeeId && x.LeaveDate <= LeaveDate && x.Year == LeaveDate.Year && x.Qty == 1 && x.IsActive == true).OrderBy(x => x.LeaveDate).FirstOrDefault();
                        if (lieu != null)
                        {
                            lieu.IsActive = false;
                            db.SaveChanges();
                        }
                    }
                }
                else if (leaveTypeCode == "COVERING")
                {
                    long levDetailId = db.Lev_LeaveDetail.Where(x => x.LeaveDate == LeaveDate && x.LeaveTypeId == LeaveTypeId && x.CoveredDay == null).Join(
                                db.Lev_LeaveHeader.Where(x => x.EmployeeId == EmployeeId),
                                ld => ld.LeaveId,
                                lh => lh.LeaveId,
                                (ld, lh) => new { Lev_LeaveDetail = ld, Lev_LeaveHeader = lh }).Select(x => x.Lev_LeaveDetail.LeaveDetailId).FirstOrDefault();

                    var levDet = db.Lev_LeaveDetail.Where(x => x.LeaveDetailId == levDetailId).FirstOrDefault();

                    if (LeaveQty == 0.5)
                    {
                        var lieu = db.Lev_CoveringLeaveDetails.Where(x => x.EmployeeId == EmployeeId && x.Qty == 0.5M && x.IsActive == true).OrderBy(x => x.LeaveDate).FirstOrDefault();
                        if (lieu != null)
                        {
                            lieu.IsActive = false;
                            if (levDet != null)
                            {
                                levDet.CoveredDay = lieu.LeaveDate;
                            }
                            db.SaveChanges();
                        }
                        else
                        {
                            var lieu2 = db.Lev_CoveringLeaveDetails.Where(x => x.EmployeeId == EmployeeId && x.Qty == 1 && x.IsActive == true).OrderBy(x => x.LeaveDate).FirstOrDefault();
                            if (lieu2 != null)
                            {
                                lieu2.Qty = 0.5M;
                                if (levDet != null)
                                {
                                    levDet.CoveredDay = lieu2.LeaveDate;
                                }
                                db.SaveChanges();
                            }
                        }
                    }
                    else
                    {
                        var lieu = db.Lev_CoveringLeaveDetails.Where(x => x.EmployeeId == EmployeeId && x.Qty == 1 && x.IsActive == true).OrderBy(x => x.LeaveDate).FirstOrDefault();
                        if (lieu != null)
                        {
                            lieu.IsActive = false;
                            if (levDet != null)
                            {
                                levDet.CoveredDay = lieu.LeaveDate;
                            }
                            db.SaveChanges();
                        }
                    }

                    if (levDet.CoveredDay != null)
                    {
                        var coveredDayTxn = db.Time_EmployeeTxn.Where(x => x.Date == levDet.CoveredDay && x.EmployeeId == EmployeeId).FirstOrDefault();
                        if (coveredDayTxn != null)
                        {
                            var coveringDayTxn = db.Time_EmployeeTxn.Where(x => x.Date == LeaveDate && x.EmployeeId == EmployeeId).FirstOrDefault();
                            if (coveredDayTxn != null)
                            {
                                coveringDayTxn.LateHrs = coveredDayTxn.LateHrs;
                                coveringDayTxn.EarlyOT = coveredDayTxn.EarlyOT;
                                coveringDayTxn.EarlyHrs = coveredDayTxn.EarlyHrs;
                                coveringDayTxn.PostOT = coveredDayTxn.PostOT;
                                coveringDayTxn.OT15 = coveredDayTxn.OT15;
                                coveringDayTxn.OT20 = coveredDayTxn.OT20;
                                coveringDayTxn.OT30 = coveredDayTxn.OT30;
                                coveringDayTxn.ApproveLateHrs = coveredDayTxn.ApproveLateHrs;
                                coveringDayTxn.ApproveEarlyOT = coveredDayTxn.ApproveEarlyOT;
                                coveringDayTxn.ApproveEarlyHrs = coveredDayTxn.ApproveEarlyHrs;
                                coveringDayTxn.ApprovePostOT = coveredDayTxn.ApprovePostOT;
                                coveringDayTxn.ApproveOT15 = coveredDayTxn.ApproveOT15;
                                coveringDayTxn.ApproveOT20 = coveredDayTxn.ApproveOT20;
                                coveringDayTxn.ApproveOT30 = coveredDayTxn.ApproveOT30;
                                coveringDayTxn.OriginalApproveOT15 = coveredDayTxn.OriginalApproveOT15;
                                coveringDayTxn.CoveringOT = coveredDayTxn.CoveringOT;
                                coveringDayTxn.IsUnauthorizedFullDay = coveredDayTxn.IsUnauthorizedFullDay;
                                coveringDayTxn.IsUnauthorizedHalfDay = coveredDayTxn.IsUnauthorizedHalfDay;
                                coveringDayTxn.Total = coveredDayTxn.Total;

                                coveredDayTxn.EarlyOT = 0;
                                coveredDayTxn.EarlyHrs = 0;
                                coveredDayTxn.PostOT = 0;
                                coveredDayTxn.OT15 = 0;
                                coveredDayTxn.OT20 = 0;
                                coveredDayTxn.OT30 = 0;
                                coveredDayTxn.ApproveLateHrs = 0;
                                coveredDayTxn.ApproveEarlyOT = 0;
                                coveredDayTxn.ApproveEarlyHrs = 0;
                                coveredDayTxn.ApprovePostOT = 0;
                                coveredDayTxn.ApproveOT15 = 0;
                                coveredDayTxn.ApproveOT20 = 0;
                                coveredDayTxn.ApproveOT30 = 0;
                                coveredDayTxn.Total = 0;
                                coveredDayTxn.IsUnauthorizedFullDay = false;
                                coveredDayTxn.IsUnauthorizedHalfDay = false;
                                coveredDayTxn.OriginalApproveOT15 = 0;
                                coveredDayTxn.IsCoveredDay = true;

                                db.SaveChanges();
                            }
                        }
                    }
                }
                else if (leaveTypeCode == "COVERINGOT")
                {
                    var totalCovOT = db.Time_TotalCoveringOT.Where(x => x.EmployeeId == EmployeeId && x.Year == LeaveDate.Year).FirstOrDefault();
                    if (totalCovOT != null)
                    {
                        int intOT = GetDecimalToInt(totalCovOT.Utilized) + GetDecimalToInt(DeductOT);
                        string rightSide = (intOT % 60).ToString();
                        if (rightSide.Length == 1)
                        {
                            rightSide = "0" + rightSide;
                        }
                        totalCovOT.Utilized = Convert.ToDecimal((intOT / 60).ToString() + "." + rightSide);
                        db.SaveChanges();
                    }
                }
                else if (leaveTypeCode == "DAYOFFFULL")
                {
                    if (timeTxn != null)
                    {
                        timeTxn.IsDayOff = true;
                        timeTxn.DayOffLeaveQty = 1;
                        db.SaveChanges();
                    }
                }
                else if (leaveTypeCode == "DAYOFFHALF")
                {
                    if (timeTxn != null)
                    {
                        timeTxn.IsDayOff = true;
                        timeTxn.DayOffLeaveQty = 0.5M;
                        db.SaveChanges();
                    }
                }
                else
                {
                    var levEnt = db.Lev_LeaveEntitlement.Where(le => le.EmployeeId == EmployeeId && le.Year == LeaveDate.Year && le.LeaveTypeId == LeaveTypeId).SingleOrDefault();
                    var levHd = db.Lev_LeaveHeader.Where(le => le.EmployeeId == EmployeeId && le.LeaveNo == LeaveNo && le.LeaveTypeId == LeaveTypeId).Select(x=>x.Status).SingleOrDefault();
                    long? levId = db.Lev_LeaveHeader.Where(le => le.EmployeeId == EmployeeId && le.LeaveNo == LeaveNo && le.LeaveTypeId == LeaveTypeId).Select(x=>x.LeaveId).SingleOrDefault();
                    var levDt = db.Lev_LeaveDetail.Where(x => x.LeaveId == levId).Select(x => x.LeaveStatus).FirstOrDefault();
                    if (levEnt != null && levHd=="Rejected")
                    {
                        levEnt.Utilized = levEnt.Utilized - (decimal)LeaveQty;
                        db.SaveChanges();
                    }
                    else if(levEnt != null && levDt == "Pending")
                    {
                        levEnt.Utilized = levEnt.Utilized + (decimal)LeaveQty;
                        db.SaveChanges();
                    }
                }
                if (LeaveQty == 1)
                {
                    if (timeTxn != null)
                    {
                        timeTxn.ApproveOT15 = 0;
                        timeTxn.ApproveEarlyHrs = 0;
                        timeTxn.ApproveEarlyOT = 0;
                        timeTxn.ApproveLateHrs = 0;
                        timeTxn.ApproveNightOT = 0;
                        timeTxn.ApproveOT20 = 0;
                        timeTxn.ApproveOT30 = 0;
                        timeTxn.OutInShiftHrs = 0;
                        timeTxn.Total = 0;
                        db.SaveChanges();
                    }
                    if (timeTxn.IsUnauthorizedFullDay)
                    {
                        timeTxn.IsUnauthorizedFullDay = false;
                       timeTxn.IsUnauthorizedHalfDay = true;
                        db.SaveChanges();
                    }
                    else
                    {
                        timeTxn.IsUnauthorizedFullDay = false;
                        timeTxn.IsUnauthorizedHalfDay = false;
                        db.SaveChanges();

                    }
                }
                var levHdStatus = db.Lev_LeaveHeader.Where(le => le.EmployeeId == EmployeeId && le.LeaveNo == LeaveNo && le.LeaveTypeId == LeaveTypeId).Select(x => x.Status).SingleOrDefault();
                long? levIdH = db.Lev_LeaveHeader.Where(le => le.EmployeeId == EmployeeId && le.LeaveNo == LeaveNo && le.LeaveTypeId == LeaveTypeId).Select(x => x.LeaveId).SingleOrDefault();
                //var levDt = db.Lev_LeaveDetail.Where(x => x.LeaveId == levId).Select(x => x.LeaveStatus).FirstOrDefault();
                var levDtStatus = db.Lev_LeaveDetail.Where(x => x.LeaveId == levIdH).Select(x => x.LeaveStatus).FirstOrDefault();
                var status = db.Lev_LeaveHeader.Where(le => le.EmployeeId == EmployeeId && le.LeaveNo == LeaveNo && le.LeaveTypeId == LeaveTypeId).Select(x => x.Status).SingleOrDefault();
                if (timeTxn.LeaveNo == null && levDtStatus == "Approved")
                {
                    timeTxn.LeaveNo = LeaveNo;
                    timeTxn.LeaveTypeId = LeaveTypeId;
                    timeTxn.LeaveQty1 = (decimal)LeaveQty;
                    timeTxn.LeaveType = leaveTypeCode;
                    timeTxn.LeaveQty = timeTxn.LeaveQty + (decimal)LeaveQty;
                    db.SaveChanges();
                }   
               /* else
                {
                    *//*timeTxn.LeaveNo2 = LeaveNo;*//*
                    timeTxn.LeaveTypeId2 = LeaveTypeId;
                    *//*timeTxn.LeaveQty2 = (decimal)LeaveQty;*/
                    /*timeTxn.LeaveType2 = leaveTypeCode;*//*
                    timeTxn.LeaveQty = timeTxn.LeaveQty + (decimal)LeaveQty;
                    db.SaveChanges();
                }*/
                if (LeaveQty == 0.5)
                {
                    if (timeTxn.IsUnauthorizedFullDay)
                    {
                        timeTxn.IsUnauthorizedFullDay = false;
                        timeTxn.IsUnauthorizedHalfDay = true;
                    }
                    else
                    {
                        timeTxn.IsUnauthorizedFullDay = false;
                        timeTxn.IsUnauthorizedHalfDay = false;
                        timeTxn.ApproveEarlyHrs = 0;
                        timeTxn.ApproveLateHrs = 0;


                    }
                }
                if (leaveTypeCode == "COVERING")
                {
                    TimeReCalculate(timeTxn, CreatedUser);
                }
            }
            catch (Exception ex)
            {

            }
   
        }

        public int GetDecimalToInt(decimal? Value)
        {
            string[] arr = Value.ToString().Split('.');
            int intValue = (Convert.ToInt32(arr[0]) * 60) + Convert.ToInt32(arr[1]);
            return intValue;
        }

        public void TimeReCalculate(Time_EmployeeTxn timeTxn, string createdUser)
        {
            string employeeCode = db.Employees.Where(x => x.EmployeeId == timeTxn.EmployeeId).Select(x => x.EmployeeCode).FirstOrDefault();
            int companyId = (int)db.Employees.Where(x => x.EmployeeId == timeTxn.EmployeeId).Select(x => x.CompanyID).FirstOrDefault();
            var rowData = db.Time_RawData.Where(x => x.CardNo == employeeCode && x.CalculatedDate == timeTxn.Date).Select(x => new {
                x.CardNo,
                x.PunchDateTime,
                x.CardTime
            }).ToList();
            var isCoveringDayObj = db.Lev_LeaveHeader.Where(x => x.EmployeeId == timeTxn.EmployeeId).Join(
                        db.Lev_LeaveDetail,
                        lh => lh.LeaveId,
                        ld => ld.LeaveId,
                        (lh, ld) => new { Lev_LeaveHeader = lh, Lev_LeaveDetail = ld }).Join(
                        db.Lev_LeaveType.Where(x => x.LeaveCode == "COVERING"),
                        ld => ld.Lev_LeaveDetail.LeaveTypeId,
                        lt => lt.LeaveTypeId,
                        (ld, lt) => new { ld.Lev_LeaveHeader, ld.Lev_LeaveDetail, Lev_LeaveType = lt }).FirstOrDefault();
            if (rowData.Count > 0)
            {
                db.Time_RawData.RemoveRange(db.Time_RawData.Where(x => x.CalculatedDate == timeTxn.Date && x.CardNo == employeeCode));
                db.SaveChanges();

                if (isCoveringDayObj == null)
                {
                    timeTxn.FirstInTime = null;
                    timeTxn.FirstOutTime = null;
                    timeTxn.LateHrs = 0;
                    timeTxn.EarlyOT = 0;
                    timeTxn.EarlyHrs = 0;
                    timeTxn.PostOT = 0;
                    timeTxn.OT15 = 0;
                    timeTxn.OT20 = 0;
                    timeTxn.OT30 = 0;
                    timeTxn.NightOT = 0;
                    timeTxn.OutInShiftHrs = 0;
                    timeTxn.ApproveLateHrs = 0;
                    timeTxn.ApproveEarlyOT = 0;
                    timeTxn.ApproveEarlyHrs = 0;
                    timeTxn.ApprovePostOT = 0;
                    timeTxn.ApproveOT15 = 0;
                    timeTxn.ApproveOT20 = 0;
                    timeTxn.ApproveOT30 = 0;
                    timeTxn.ApproveNightOT = 0;
                    timeTxn.WorkingHrs = 0;
                    timeTxn.Total = 0;
                    db.SaveChanges();
                }

                foreach (var row in rowData)
                {
                    var affectedRows = db.Database.ExecuteSqlCommand("Time_ModifyAttendance @EmployeeCode,@Day,@Month,@Year,@attType,@punchTime,@ShiftId,@CreatedUser,@RelevantDay,@RelevantMonth,@RelevantYear,@CompanyId",
                        new SqlParameter("@EmployeeCode", employeeCode),
                        new SqlParameter("@Day", row.PunchDateTime.Day),
                        new SqlParameter("@Month", row.PunchDateTime.Month),
                        new SqlParameter("@Year", row.PunchDateTime.Year),
                        new SqlParameter("@attType", "a"),
                        new SqlParameter("@punchTime", row.CardTime),
                        new SqlParameter("@ShiftId", "0"),
                        new SqlParameter("@CreatedUser", createdUser),
                        new SqlParameter("@RelevantDay", timeTxn.Date.Day),
                        new SqlParameter("@RelevantMonth", timeTxn.Date.Month),
                        new SqlParameter("@RelevantYear", timeTxn.Date.Year),
                        new SqlParameter("@CompanyId", companyId));
                }
            }
        }

        public ActionResult SaveMedicalLeaveAttchement()
        {
            bool result = false;
            try
            {
                int EmployeeId = Convert.ToInt32(Request.Form["Employeeid"].ToString());
                int CompanyId = (int)db.Employees.Where(x => x.EmployeeId == EmployeeId).Select(x => x.CompanyID).SingleOrDefault();
                string LeaveNo = GetCurrentLeaveNo("LeaveApplication", CompanyId);
                Lev_LeaveHeader leaveHeader = db.Lev_LeaveHeader.Where(x => x.LeaveNo == LeaveNo).SingleOrDefault();
                for (int i = 0; i < Request.Files.Count; i++)
                {

                    var file = Request.Files[i];
                    string fileName = Path.GetFileNameWithoutExtension(file.FileName);
                    if (fileName.Length > 0)
                    {
                        string extension = Path.GetExtension(file.FileName);
                        fileName = fileName + DateTime.Now.ToString("yymmssfff") + "-" + LeaveNo + extension;

                        leaveHeader.AttachmentParth = "~/AppFiles/Leave/" + fileName;
                        leaveHeader.IsAttachemetAvailable = true;
                        db.SaveChanges();
                        fileName = Path.Combine(Server.MapPath("~/AppFiles/Leave/"), fileName);
                        file.SaveAs(fileName);
                    }
                    else
                    {
                        leaveHeader.IsAttachemetAvailable = false;
                        db.SaveChanges();
                    }

                    result = true;
                    // save file as required here...
                }
                if (Request.Files.Count == 0)
                {
                    leaveHeader.IsAttachemetAvailable = false;
                    db.SaveChanges();
                }

                return Json(new { success = result, message = "Submitted Succesfully" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = result, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public List<String> GetCCList()
        {
            var ccList = db.mks_SysUsers.Where(x => x.UserName != "Infox" && x.IsBlock == false).Select(x => x.Email).ToList();
            return ccList;
        }

        public void SendEmailToCoveringPerson(long LeaveId, long CoverinPersonId)
        {
            StringWriter writer = new StringWriter();
            HtmlTextWriter html = new HtmlTextWriter(writer);
            string senderEmail = ConfigurationManager.AppSettings["LeaveEmail"];

            var leaveDetail = db.Lev_LeaveHeader.Where(x => x.LeaveId == LeaveId).Select(x => new
            {
                x.EmployeeId,
                x.FromDate,
                x.Todate
            }).FirstOrDefault();

            if (leaveDetail != null)
            {
                var leaveEmployeeName = db.Employees.Where(x => x.EmployeeId == leaveDetail.EmployeeId).Join(
                            db.CompanyProfiles,
                            em => em.CompanyID,
                            co => co.CompanyID,
                            (em, co) => new { Employee = em, CompanyProfile = co }).Select(x =>
                             x.CompanyProfile.EmployeeReportViewName == 1 ? x.Employee.EmployeeCode + " | " + x.Employee.FirstName :
                             x.CompanyProfile.EmployeeReportViewName == 2 ? x.Employee.EmployeeCode + " | " + x.Employee.LastName :
                             x.CompanyProfile.EmployeeReportViewName == 3 ? x.Employee.EmployeeCode + " | " + x.Employee.CallName :
                             x.CompanyProfile.EmployeeReportViewName == 4 ? x.Employee.EmployeeCode + " | " + x.Employee.NameWithInitial + " " + x.Employee.LastName :
                             x.CompanyProfile.EmployeeReportViewName == 5 ? x.Employee.EPFNo + " | " + x.Employee.FirstName :
                             x.CompanyProfile.EmployeeReportViewName == 6 ? x.Employee.EPFNo + " | " + x.Employee.LastName :
                             x.CompanyProfile.EmployeeReportViewName == 7 ? x.Employee.EPFNo + " | " + x.Employee.CallName :
                             x.Employee.EPFNo + " | " + x.Employee.NameWithInitial + " " + x.Employee.LastName).FirstOrDefault();
                var coveringEmail = db.Employees.Where(x => x.EmployeeId == CoverinPersonId).Select(x => new
                {
                    x.Email,
                    CoveringPersonName = x.FirstName + " " + x.LastName
                }).FirstOrDefault();

                Style style = new Style();
                style.Font.Name = "Verdana";
                style.Font.Size = 10;
                style.Font.Bold = false;
                html.EnterStyle(style);
                html.RenderBeginTag(HtmlTextWriterTag.P);
                html.WriteEncodedText(string.Format("Dear {0},", coveringEmail.CoveringPersonName));
                html.WriteBreak();
                html.RenderBeginTag(HtmlTextWriterTag.P);

                html.WriteEncodedText(string.Format("{0} has requested for covering from {1} to {2} for his/her absence.", leaveEmployeeName, leaveDetail.FromDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), leaveDetail.Todate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
                html.WriteEncodedText(" Please log into the Employee Self Service Portal to accept the request.");
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
                html.Write("Click Here to Log ESS Portal: <a href='https://hrm.sportingstar.lk:3065/'>https://hrm.sportingstar.lk:3065/</a>");
                html.WriteBreak();
                html.WriteEncodedText("Please note that this email is automatically generated from the system, therefore, do not need to reply.");
                html.WriteBreak();
                html.Flush();
                string htmlString = writer.ToString();
                string subject = "Leave Covering Request from " + leaveEmployeeName;

                CRUD_OperationByMeUsingJqueryAjaxMvc.Email.Email email = new CRUD_OperationByMeUsingJqueryAjaxMvc.Email.Email();
                email.SendemailInExchangeServer(senderEmail, coveringEmail.Email, subject, htmlString, GetCCList());
            }
        }
        public void SendEmailToAppliedEmp(long EmployeeId, long ApprovePersonId, DateTime leaveDate)
        {
            StringWriter writer = new StringWriter();
            HtmlTextWriter html = new HtmlTextWriter(writer);
            string senderEmail = ConfigurationManager.AppSettings["LeaveEmail"];



            var EmployeeEmail = db.Employees.Where(x => x.EmployeeId == EmployeeId).Select(x => new
            {
                x.Email,
                Name = x.EmployeeCode + "-" + x.FirstName + " " + x.LastName
            }).FirstOrDefault();

            var approveEmail = db.Employees.Where(x => x.EmployeeId == ApprovePersonId).Select(x => new
            {
                x.Email,
                ApprovePersonName = x.FirstName + " " + x.LastName
            }).FirstOrDefault();

            Style style = new Style();
            style.Font.Name = "Verdana";
            style.Font.Size = 10;
            style.Font.Bold = false;
            html.EnterStyle(style);
            html.RenderBeginTag(HtmlTextWriterTag.P);
            html.WriteEncodedText(string.Format("Dear {0},", EmployeeEmail.Name));
            html.WriteBreak();
            html.RenderBeginTag(HtmlTextWriterTag.P);

            html.WriteEncodedText(string.Format("We would like to inform you that {0} has approved the leave request for you on {1}.", approveEmail.ApprovePersonName, leaveDate));
            html.WriteEncodedText(" Kindly log in to the Employee Self-Service Portal to review the approved leave.");
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
            html.Write("Click Here to Log ESS Portal: <a href='https://hrm.sportingstar.lk:3065/'>https://hrm.sportingstar.lk:3065/</a>");
            html.WriteBreak();
            html.WriteEncodedText("Kindly be advised that this email has been generated automatically by the system, Do not need to reply.");
            html.WriteBreak();
            html.Flush();
            string htmlString = writer.ToString();
            string subject = "Leave Approved from " + approveEmail.ApprovePersonName;

            CRUD_OperationByMeUsingJqueryAjaxMvc.Email.Email email = new CRUD_OperationByMeUsingJqueryAjaxMvc.Email.Email();

            if (approveEmail.Email != "" && approveEmail.Email != null)
                email.SendemailInExchangeServer(senderEmail, approveEmail.Email, subject, htmlString, GetCCList());
            //email.SendemailInExchangeServer("isharaz2810@gmail.com", "ishara@infoxglobal.com", subject, htmlString, "");

        }

        public void SendEmailToApprovePerson(long LeaveId, long ApprovePersonId)
        {
            StringWriter writer = new StringWriter();
            HtmlTextWriter html = new HtmlTextWriter(writer);
            string senderEmail = ConfigurationManager.AppSettings["LeaveEmail"];

            var leaveDetail = db.Lev_LeaveHeader.Where(x => x.LeaveId == LeaveId).Select(x => new
            {
                x.EmployeeId,
                x.FromDate,
                x.Todate
            }).FirstOrDefault();

            if (leaveDetail != null)
            {
                var leaveEmployeeName = db.Employees.Where(x => x.EmployeeId == leaveDetail.EmployeeId).Join(
                            db.CompanyProfiles,
                            em => em.CompanyID,
                            co => co.CompanyID,
                            (em, co) => new { Employee = em, CompanyProfile = co }).Select(x =>
                             x.CompanyProfile.EmployeeReportViewName == 1 ? x.Employee.EmployeeCode + " | " + x.Employee.FirstName :
                             x.CompanyProfile.EmployeeReportViewName == 2 ? x.Employee.EmployeeCode + " | " + x.Employee.LastName :
                             x.CompanyProfile.EmployeeReportViewName == 3 ? x.Employee.EmployeeCode + " | " + x.Employee.CallName :
                             x.CompanyProfile.EmployeeReportViewName == 4 ? x.Employee.EmployeeCode + " | " + x.Employee.NameWithInitial + " " + x.Employee.LastName :
                             x.CompanyProfile.EmployeeReportViewName == 5 ? x.Employee.EPFNo + " | " + x.Employee.FirstName :
                             x.CompanyProfile.EmployeeReportViewName == 6 ? x.Employee.EPFNo + " | " + x.Employee.LastName :
                             x.CompanyProfile.EmployeeReportViewName == 7 ? x.Employee.EPFNo + " | " + x.Employee.CallName :
                             x.Employee.EPFNo + " | " + x.Employee.NameWithInitial + " " + x.Employee.LastName).FirstOrDefault();
                var approveEmail = db.Employees.Where(x => x.EmployeeId == ApprovePersonId).Select(x => new
                {
                    x.Email,
                    ApprovePersonName = x.FirstName + " " + x.LastName
                }).FirstOrDefault();

                Style style = new Style();
                style.Font.Name = "Verdana";
                style.Font.Size = 10;
                style.Font.Bold = false;
                html.EnterStyle(style);
                html.RenderBeginTag(HtmlTextWriterTag.P);
                html.WriteEncodedText(string.Format("Dear {0},", approveEmail.ApprovePersonName));
                html.WriteBreak();
                html.RenderBeginTag(HtmlTextWriterTag.P);

                html.WriteEncodedText(string.Format("{0} has requested for leave from {1} to {2}.", leaveEmployeeName, leaveDetail.FromDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), leaveDetail.Todate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
                html.WriteEncodedText(" Please log into the Employee Self Service Portal to accept the request.");
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
                html.Write("Click Here to Log ESS Portal: <a href='https://hrm.sportingstar.lk:3065/'>https://hrm.sportingstar.lk:3065/</a>");
                html.WriteBreak();
                html.WriteEncodedText("Please note that this email is automatically generated from the system, therefore, do not need to reply.");
                html.WriteBreak();
                html.Flush();
                string htmlString = writer.ToString();
                string subject = "Leave Approve Request from " + leaveEmployeeName;

                CRUD_OperationByMeUsingJqueryAjaxMvc.Email.Email email = new CRUD_OperationByMeUsingJqueryAjaxMvc.Email.Email();

                if (approveEmail.Email != "" && approveEmail.Email != null)
                    email.SendemailInExchangeServer(senderEmail, approveEmail.Email, subject, htmlString, GetCCList());
            }
        }

        public void SendEmailWhenLeaveRejected(long LeaveId, long ApprovePersonId)
        {
            StringWriter writer = new StringWriter();
            HtmlTextWriter html = new HtmlTextWriter(writer);
            string senderEmail = ConfigurationManager.AppSettings["LeaveEmail"];

            var leaveDetail = db.Lev_LeaveHeader.Where(x => x.LeaveId == LeaveId).Select(x => new
            {
                x.EmployeeId,
                x.FromDate,
                x.Todate
            }).FirstOrDefault();

            if (leaveDetail != null)
            {
                var leaveEmployeeName = db.Employees.Where(x => x.EmployeeId == leaveDetail.EmployeeId).Select(x => new
                {
                    LeaveEmployeeName = x.FirstName + " " + x.LastName,
                    x.Email
                }).FirstOrDefault();

                var approveEmail = db.Employees.Where(x => x.EmployeeId == ApprovePersonId).Select(x => new
                {
                    x.Email,
                    ApprovePersonName = x.FirstName + " " + x.LastName
                }).FirstOrDefault();

                Style style = new Style();
                style.Font.Name = "Verdana";
                style.Font.Size = 10;
                style.Font.Bold = false;
                html.EnterStyle(style);
                html.RenderBeginTag(HtmlTextWriterTag.P);
                html.WriteEncodedText(string.Format("Dear {0},", leaveEmployeeName.LeaveEmployeeName));
                html.WriteBreak();
                html.RenderBeginTag(HtmlTextWriterTag.P);

                html.WriteEncodedText(string.Format("From {0} to {1} leaves that you requested have rejected by {2}.", leaveDetail.FromDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), leaveDetail.Todate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), approveEmail.ApprovePersonName));
                html.WriteEncodedText(" Please log into the Employee Self Service Portal to get more details.");
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
                html.Write("Click Here to Log ESS Portal: <a href='https://hrm.sportingstar.lk:3065/'>https://hrm.sportingstar.lk:3065/</a>");
                html.WriteBreak();
                html.WriteEncodedText("Please note that this email is automatically generated from the system, therefore, do not need to reply.");
                html.WriteBreak();
                html.Flush();
                string htmlString = writer.ToString();
                string subject = "Leave Approve Request from " + leaveEmployeeName;

                CRUD_OperationByMeUsingJqueryAjaxMvc.Email.Email email = new CRUD_OperationByMeUsingJqueryAjaxMvc.Email.Email();

                if (leaveEmployeeName.Email != "" && leaveEmployeeName.Email != null)
                    email.SendemailInExchangeServer(senderEmail, leaveEmployeeName.Email, subject, htmlString, GetCCList());
                //email.SendemailInExchangeServer("isharaz2810@gmail.com", "ishara@infoxglobal.com", subject, htmlString, "");
            }
        }

        public void InsertLeaveNotificationForCovering(long EmployeeId, long LeaveEmployeeId, bool IsCovering)
        {
            string content = "";
            string functionName = "";
            var leaveEmployeeName = db.Employees.Where(x => x.EmployeeId == LeaveEmployeeId).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault();
            if (IsCovering)
            {
                content = leaveEmployeeName + " has requested for covering for his/her absence.";
                functionName = "LeaveCovering";
            }
            else
            {
                content = leaveEmployeeName + " has requested for leave.";
                functionName = "LeaveApprove";
            }

            Por_Notification noti = new Por_Notification();
            noti.EmployeeId = EmployeeId;
            noti.NotificationTypeId = 1;
            noti.Content = content;
            noti.ControllerName = "Leave";
            noti.FunctionName = functionName;
            noti.IsExpired = false;
            noti.CreatedDate = DateTime.Now;
            db.Por_Notification.Add(noti);
            db.SaveChanges();
        }
        public void InsertLeaveNotificationForApprovePerson(long EmployeeId, long ApprovePerson1, DateTime FromDate, DateTime ToDate)
        {
            string content = "";
            string functionName = "";
            var LeaveEmployeeName = db.Employees.Where(x => x.EmployeeId == EmployeeId).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault();


            content = LeaveEmployeeName+" is requested leave from " + FromDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + " to " + ToDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) +  ".";
            functionName = "LeaveApprove";


            Por_Notification noti = new Por_Notification();
            noti.EmployeeId = ApprovePerson1;
            noti.NotificationTypeId = 4;
            noti.Content = content;
            noti.ControllerName = "Leave";
            noti.FunctionName = functionName;
            noti.IsExpired = false;
            noti.CreatedDate = DateTime.Now;
            db.Por_Notification.Add(noti);
            db.SaveChanges();
        }

        public void InsertLeaveNotificationWhenReject(long RejectEmployeeId, long LeaveEmployeeId, DateTime FromDate, DateTime ToDate)
        {
            string content = "";
            string functionName = "";
            var rejectedEmployeeName = db.Employees.Where(x => x.EmployeeId == RejectEmployeeId).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault();
            content = "From " + FromDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + " to " + ToDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + " leaves have rejected by " + rejectedEmployeeName + ".";
            functionName = "LeaveHistory";

            Por_Notification noti = new Por_Notification();
            noti.EmployeeId = LeaveEmployeeId;
            noti.NotificationTypeId = 1;
            noti.Content = content;
            noti.ControllerName = "Leave";
            noti.FunctionName = functionName;
            noti.IsExpired = false;
            noti.CreatedDate = DateTime.Now;
            db.Por_Notification.Add(noti);
            db.SaveChanges();
        }

        public JsonResult GetLeaveBalance(string fromDate, int LeaveTypeId, int EmployeeId, string toDate)
        {
            var result = false;
            try
            {
                DateTime FromDate = dateConvert.GetDateToAll(fromDate);
                DateTime ToDate = dateConvert.GetDateToAll(toDate);
                var list = db.Database.SqlQuery<GetRemainingLeave>(
                   "exec dbo.[Lev_GetRemainingAnnualLeave] @EmployeeId,@Year,@Month,@LeaveTypeId,@ToDate",
                    new Object[] {
                    new SqlParameter("@EmployeeId", EmployeeId),
                    new SqlParameter("@Year", FromDate.Year),
                    new SqlParameter("@Month", FromDate.Month),
                    new SqlParameter("@LeaveTypeId", LeaveTypeId),
                    new SqlParameter("@ToDate", ToDate)}
                    ).FirstOrDefault();

                result = true;

                return Json(new { success = result, message = list.RemainingCount.ToString() }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = result, message = "0" }, JsonRequestBehavior.AllowGet);
            }

        }

        public ActionResult LeaveCovering()
        {
            return View();
        }


        public ActionResult LeaveApprove()
        {
            return View();
        }

        public ActionResult GetPendingCoveringLeave(int EmployeeId)
        {
            db.Configuration.ProxyCreationEnabled = false;
            var covLeaves =
                db.Lev_LeaveHeader.Where(lh => lh.CoveringPerson == EmployeeId && lh.CoveringPersonStatus == "Pending").
                Join(db.Employees, lh => lh.EmployeeId, em => em.EmployeeId,
                (lh, em) => new { Lev_LeaveHeader = lh, EmpName = em.FirstName + " " + em.LastName }).ToList().Select(m => new
                {
                    m.Lev_LeaveHeader.LeaveId,
                    FromDate = m.Lev_LeaveHeader.FromDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    ToDate = m.Lev_LeaveHeader.Todate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    LeaveQty = m.Lev_LeaveHeader.TotalLeaveQty,
                    EmployeeName = m.EmpName,
                }).ToList();

            return Json(new { data = covLeaves }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetConfirmCoveringLeave(int EmployeeId)
        {
            db.Configuration.ProxyCreationEnabled = false;
            var covLeaves =
                db.Lev_LeaveHeader.Where(lh => lh.CoveringPerson == EmployeeId && lh.CoveringPersonStatus == "Approved").
                Join(db.Employees, lh => lh.EmployeeId, em => em.EmployeeId,
                (lh, em) => new { Lev_LeaveHeader = lh, EmpName = em.FirstName + " " + em.LastName }).ToList().Select(m => new
                {
                    m.Lev_LeaveHeader.LeaveId,
                    FromDate = m.Lev_LeaveHeader.FromDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    ToDate = m.Lev_LeaveHeader.Todate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    LeaveQty = m.Lev_LeaveHeader.TotalLeaveQty,
                    EmployeeName = m.EmpName,
                    m.Lev_LeaveHeader.CoveringPersonStatus
                }).ToList();

            return Json(new { data = covLeaves }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetRejectedCoveringLeave(int EmployeeId)
        {
            db.Configuration.ProxyCreationEnabled = false;
            var covLeaves =
                db.Lev_LeaveHeader.Where(lh => lh.CoveringPerson == EmployeeId && lh.CoveringPersonStatus == "Rejected").
                Join(db.Employees, lh => lh.EmployeeId, em => em.EmployeeId,
                (lh, em) => new { Lev_LeaveHeader = lh, EmpName = em.FirstName + " " + em.LastName }).ToList().Select(m => new
                {
                    m.Lev_LeaveHeader.LeaveId,
                    FromDate = m.Lev_LeaveHeader.FromDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    ToDate = m.Lev_LeaveHeader.Todate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    LeaveQty = m.Lev_LeaveHeader.TotalLeaveQty,
                    EmployeeName = m.EmpName,
                    m.Lev_LeaveHeader.RejectedReason,

                }).ToList();

            return Json(new { data = covLeaves }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetEntitleLeaveSummary(int EmployeeId, int Year)
        {
            db.Configuration.ProxyCreationEnabled = false;
            var entLeaves = db.Lev_LeaveEntitlement.Where(x => x.EmployeeId == EmployeeId && x.Year == Year).Join(
                                db.Lev_LeaveType,
                                le => le.LeaveTypeId,
                                lt => lt.LeaveTypeId,
                                (le, lt) => new EntitleLeavesSummary
                                {
                                    LeaveType = lt.LeaveDescription,
                                    EntitleLeaves = le.Entitlement,
                                    UtilizedLeaves = le.Utilized,
                                    Balance = le.Entitlement - le.Utilized
                                }).ToList();

            return Json(new { data = entLeaves }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetLeaveHistory(int EmployeeId, int Year)
        {
            db.Configuration.ProxyCreationEnabled = false;
            var leaveHistory = db.Lev_LeaveDetail.Where(x => x.LeaveDate.Year == Year).Join(
                        db.Lev_LeaveHeader.Where(x => x.EmployeeId == EmployeeId),
                        ld => ld.LeaveId,
                        lh => lh.LeaveId,
                        (ld, lh) => new { Lev_LeaveDetail = ld, Lev_LeaveHeader = lh }
                        ).Join(db.Lev_LeaveType,
                        ld => ld.Lev_LeaveDetail.LeaveTypeId,
                        lt => lt.LeaveTypeId,
                        (ld, lt) => new { ld.Lev_LeaveDetail, ld.Lev_LeaveHeader, Lev_LeaveType = lt }).GroupJoin(
                        db.Lev_LeaveReason,
                        lh => lh.Lev_LeaveHeader.LeaveReason,
                        lr => lr.LeaveReasonId,
                        (lh, lr) => new { lh.Lev_LeaveDetail, lh.Lev_LeaveHeader, lh.Lev_LeaveType, Lev_LeaveReason = lr }).ToList().SelectMany(m =>
                          m.Lev_LeaveReason.DefaultIfEmpty(null),
                        (x, y) => new LeaveHistoryDetails
                        {
                            RequestedDate = x.Lev_LeaveDetail.LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                            LeaveDate = x.Lev_LeaveDetail.LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                            LeaveType = x.Lev_LeaveType.LeaveDescription,
                            LeaveQty = x.Lev_LeaveDetail.LeaveQty,
                            LeaveId = x.Lev_LeaveDetail.LeaveId,
                            Session = x.Lev_LeaveDetail.Session,
                            Satus = x.Lev_LeaveHeader.Status,
                            Reason = (y == null || y.LeaveDescription == null ? "" : y.LeaveDescription),
                            LeaveDetailId = x.Lev_LeaveDetail.LeaveDetailId
                        }).ToList().OrderBy(x => x.LeaveDate);

            return Json(new { data = leaveHistory }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetLeaveCancelationHistory(int EmployeeId)
        {
            db.Configuration.ProxyCreationEnabled = false;
            var leaveHistory = db.Lev_CancelLeaves.Join(
                        db.Lev_LeaveHeaderHistory.Where(x => x.EmployeeId == EmployeeId),
                        ld => ld.LeaveId,
                        lh => lh.LeaveId,
                        (ld, lh) => new { Lev_CancelLeaves = ld, Lev_LeaveHeaderHistory = lh }
                        ).Join(db.Lev_LeaveType,
                        ld => ld.Lev_CancelLeaves.LeaveTypeId,
                        lt => lt.LeaveTypeId,
                        (ld, lt) => new { ld.Lev_CancelLeaves, ld.Lev_LeaveHeaderHistory, Lev_LeaveType = lt }).ToList().GroupJoin(
                        db.Lev_LeaveReason,
                        lh => lh.Lev_LeaveHeaderHistory.LeaveReason,
                        lr => lr.LeaveReasonId,
                        (lh, lr) => new { lh.Lev_LeaveHeaderHistory, lh.Lev_LeaveType, lh.Lev_CancelLeaves, Lev_LeaveReason = lr }).ToList().SelectMany(m =>
                          m.Lev_LeaveReason.DefaultIfEmpty(null),
                        (x, y) => new
                        {
                            RequestedDate = x.Lev_CancelLeaves.LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                            LeaveDate = x.Lev_CancelLeaves.LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                            LeaveType = x.Lev_LeaveType.LeaveDescription,
                            LeaveQty = x.Lev_CancelLeaves.LeaveQty,
                            LeaveId = x.Lev_CancelLeaves.LeaveId,
                            Session = x.Lev_CancelLeaves.Session,
                            Satus = x.Lev_CancelLeaves.CancelStatus,
                            Reason = (y == null || y.LeaveDescription == null ? "" : y.LeaveDescription),
                            LeaveDetailId = x.Lev_CancelLeaves.LeaveDetailId,
                            x.Lev_CancelLeaves.CancelReason,
                            x.Lev_CancelLeaves.LeaveCancelId
                        }).ToList().OrderBy(x => x.LeaveDate);

            return Json(new { data = leaveHistory }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetLeaveApproval(int EmployeeId)
        {
            db.Configuration.ProxyCreationEnabled = false;
            var leaveApproval =
                        db.Lev_LeaveHeader.
                        Join(db.Lev_LeaveType,
                        ld => ld.LeaveTypeId,
                        lt => lt.LeaveTypeId,
                        (ld, lt) => new { Lev_LeaveHeader = ld, Lev_LeaveType = lt }).Join(
                        db.Com_CommonAssignData,
                        lh => lh.Lev_LeaveHeader.EmployeeId,
                        ca => ca.EmployeeId,
                        (ld, ca) => new { ld.Lev_LeaveHeader, ld.Lev_LeaveType, Com_CommonAssignData = ca }).Join(
                        db.Com_CommonApprovalWorkFlow,
                        ca => ca.Com_CommonAssignData.ApprovalWorkFlowId,
                        cw => cw.ApprovalWorkFlowId,
                        (ca, cw) => new { ca.Lev_LeaveHeader, ca.Lev_LeaveType, ca.Com_CommonAssignData, Com_CommonApprovalWorkFlow = cw }).Join(
                        db.ApprovalTypes,
                        cw => cw.Com_CommonApprovalWorkFlow.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cw, at) => new { cw.Lev_LeaveHeader, cw.Lev_LeaveType, cw.Com_CommonAssignData, cw.Com_CommonApprovalWorkFlow, ApprovalType = at }).Join(
                        db.Employees,
                        lh => lh.Lev_LeaveHeader.EmployeeId,
                        em => em.EmployeeId,
                        (lh, em) => new { lh.Lev_LeaveHeader, lh.Lev_LeaveType, lh.Com_CommonAssignData, lh.Com_CommonApprovalWorkFlow, lh.ApprovalType, Employee = em }).
                        Where(m => ((m.Com_CommonApprovalWorkFlow.ApprovePerson1 == EmployeeId && m.Lev_LeaveHeader.ApprovePerson1Status == "Pending") ||
                        (m.Com_CommonApprovalWorkFlow.ApprovePerson2 == EmployeeId && m.Lev_LeaveHeader.ApprovePerson2Status == "Pending") ||
                        (m.Com_CommonApprovalWorkFlow.ApprovePerson3 == EmployeeId && m.Lev_LeaveHeader.ApprovePerson3Status == "Pending")) &&
                        m.ApprovalType.ApprovalTypeCode == "LA" && m.Lev_LeaveHeader.IsApprovedByHR == false).ToList().GroupJoin(
                        db.Lev_LeaveReason,
                        lh => lh.Lev_LeaveHeader.LeaveReason,
                        lr => lr.LeaveReasonId,
                        (lh, lr) => new { lh.Lev_LeaveHeader, lh.Lev_LeaveType, lh.Employee, Lev_LeaveReason = lr }).ToList().SelectMany(m =>
                          m.Lev_LeaveReason.DefaultIfEmpty(null),
                        (x, y) => new
                        {
                            x.Lev_LeaveHeader.LeaveId,
                            EmployeeName = x.Employee.FirstName + " " + x.Employee.LastName,
                            FromDate = x.Lev_LeaveHeader.FromDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                            ToDate = x.Lev_LeaveHeader.Todate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                            LeaveQty = x.Lev_LeaveHeader.TotalLeaveQty,
                            x.Lev_LeaveHeader.EmployeeId,
                            Reason = (y == null || y.LeaveDescription == null ? "" : y.LeaveDescription),
                            x.Lev_LeaveType.LeaveDescription
                        }).ToList();

            return Json(new { data = leaveApproval }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult CoveringPersonApprove(int LeaveId)
        {
            var result = false;
            try
            {
                var leave = db.Lev_LeaveHeader.Where(le => le.LeaveId == LeaveId).SingleOrDefault();
                if (leave != null)
                {
                    var levWF = db.Com_CommonApprovalWorkFlow.Join(
                        db.Com_CommonAssignData,
                        cap => cap.ApprovalWorkFlowId,
                        cas => cas.ApprovalWorkFlowId,
                        (cap, cas) => new { Com_CommonApprovalWorkFlow = cap, Com_CommonAssignData = cas }).Join(
                        db.ApprovalTypes,
                        cas => cas.Com_CommonAssignData.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cas, at) => new { cas.Com_CommonApprovalWorkFlow, cas.Com_CommonAssignData, ApprovalType = at }).Where(
                        x => x.ApprovalType.ApprovalTypeCode == "LA" && x.Com_CommonAssignData.EmployeeId == leave.EmployeeId).Select(v => new
                        {
                            v.Com_CommonApprovalWorkFlow.ApprovePerson1,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson2,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson3
                        }).ToList();

                    leave.CoveringPersonStatus = "Approved";
                    //leave.ApprovePerson1Status = "Pending";   --------------2022-06-01 Requested by edna to send approval for approve person without considering covering person
                    leave.ApprovePerson1 = levWF.Select(x => x.ApprovePerson1).FirstOrDefault();
                    db.SaveChanges();

                    //InsertLeaveNotification((long)levWF.Select(x => x.ApprovePerson1).FirstOrDefault(), leave.EmployeeId, false);
                    SendEmailToApprovePerson(LeaveId, (long)levWF.Select(x => x.ApprovePerson1).FirstOrDefault());

                    result = true;

                    return Json(new { success = result, message = "Submitted Successfully" }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = result, message = "Submitted Failed" }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = result, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult CoveringPersonReject(int LeaveId, int EmployeeId, string RejectReason)
        {
            var result = false;
            try
            {
                var leave = db.Lev_LeaveHeader.Where(le => le.LeaveId == LeaveId).SingleOrDefault();
                if (leave != null)
                {
                    leave.CoveringPersonStatus = "Rejected";
                    leave.Status = "Rejected";
                    leave.RejectedReason = RejectReason;
                    leave.RejectedBy = EmployeeId;
                    db.SaveChanges();
                    result = true;

                    InsertLeaveNotificationWhenReject(EmployeeId, leave.EmployeeId, leave.FromDate, leave.Todate);
                    SendEmailWhenLeaveRejected(LeaveId, EmployeeId);

                    return Json(new { success = result, message = "Submitted Successfully" }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = result, message = "Submitted Failed" }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = result, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult CheckIsFinalApprove(int LeaveId, long EmployeeId)
        {
            try
            {
                bool show = false;
                long leaveEmployeeId = db.Lev_LeaveHeader.Where(x => x.LeaveId == LeaveId).Select(x => x.EmployeeId).FirstOrDefault();
                var levWF = db.Com_CommonApprovalWorkFlow.Join(
                        db.Com_CommonAssignData,
                        cap => cap.ApprovalWorkFlowId,
                        cas => cas.ApprovalWorkFlowId,
                        (cap, cas) => new { Com_CommonApprovalWorkFlow = cap, Com_CommonAssignData = cas }).Join(
                        db.ApprovalTypes,
                        cas => cas.Com_CommonAssignData.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cas, at) => new { cas.Com_CommonApprovalWorkFlow, cas.Com_CommonAssignData, ApprovalType = at }).Where(
                        x => x.ApprovalType.ApprovalTypeCode == "LA" && x.Com_CommonAssignData.EmployeeId == leaveEmployeeId).Select(v => new
                        {
                            v.Com_CommonApprovalWorkFlow.ApprovePerson1,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson2,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson3
                        }).FirstOrDefault();

                if ((levWF.ApprovePerson3 == EmployeeId) || (levWF.ApprovePerson3 == null && levWF.ApprovePerson2 == EmployeeId) || levWF.ApprovePerson2 == null && levWF.ApprovePerson1 == EmployeeId)
                {
                    show = true;
                }

                return Json(new { success = true, show }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult ApprovePersonApprove(int LeaveId, string CreatedUser, bool IsApprovedLeave)
        {
            var result = false;
            try
            {
                long leaveEmployeeId = 0;
                string aprvPerson1Status = "";
                string aprvPerson2Status = "";
                string aprvPerson3Status = "";
                string approvePerson1 = "";
                string approvePerson2 = "";
                string approvePerson3 = "";
                string leaveNo = "";
                DateTime leaveDate= new DateTime();
                int leaveTypeId;
                decimal leaveQty;



                var lev = db.Lev_LeaveHeader.Where(x => x.LeaveId == LeaveId).
                        Select(v => new
                        {
                            v.EmployeeId,
                            v.ApprovePerson1Status,
                            v.ApprovePerson2Status,
                            v.ApprovePerson3Status,
                            v.LeaveTypeId,
                            v.LeaveNo,
                            v.FromDate
                         
                        }
                        ).ToList();

                leaveEmployeeId = lev.Select(x => x.EmployeeId).SingleOrDefault();
                leaveDate = lev.Select(x => x.FromDate).SingleOrDefault();

                var levWF = db.Com_CommonApprovalWorkFlow.Join(
                        db.Com_CommonAssignData,
                        cap => cap.ApprovalWorkFlowId,
                        cas => cas.ApprovalWorkFlowId,
                        (cap, cas) => new { Com_CommonApprovalWorkFlow = cap, Com_CommonAssignData = cas }).Join(
                        db.ApprovalTypes,
                        cas => cas.Com_CommonAssignData.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cas, at) => new { cas.Com_CommonApprovalWorkFlow, cas.Com_CommonAssignData, ApprovalType = at }).Where(
                        x => x.ApprovalType.ApprovalTypeCode == "LA" && x.Com_CommonAssignData.EmployeeId == leaveEmployeeId).Select(v => new
                        {
                            v.Com_CommonApprovalWorkFlow.ApprovePerson1,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson2,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson3
                        }).ToList();


                leaveTypeId = lev.Select(x => x.LeaveTypeId).SingleOrDefault();
                aprvPerson1Status = lev.Select(x => x.ApprovePerson1Status).SingleOrDefault();
                aprvPerson2Status = lev.Select(x => x.ApprovePerson2Status).SingleOrDefault();
                aprvPerson3Status = lev.Select(x => x.ApprovePerson3Status).SingleOrDefault();
                leaveNo = lev.Select(x => x.LeaveNo).SingleOrDefault();
                approvePerson1 = levWF.Select(x => x.ApprovePerson1).FirstOrDefault().ToString();
                approvePerson2 = levWF.Select(x => x.ApprovePerson2).FirstOrDefault().ToString();
                approvePerson3 = levWF.Select(x => x.ApprovePerson3).FirstOrDefault().ToString();
                var levId = db.Lev_LeaveHeader.Where(x => x.LeaveNo == leaveNo).Select(x => x.LeaveId).SingleOrDefault();
                decimal qty = db.Lev_LeaveHeader.Where(x => x.LeaveId == levId).Select(x => x.TotalLeaveQty).SingleOrDefault();
                //var deductedOT = db.Lev_LeaveDetail.Where(x => x.LeaveId == levId).Select(x => x.DeductedOT).SingleOrDefault();

                if (lev != null && levWF != null)
                {
                    var ld = db.Lev_LeaveHeader.Where(x => x.LeaveId == LeaveId).FirstOrDefault();

                    var leaveDetail = db.Lev_LeaveDetail.Where(x => x.LeaveId == LeaveId).ToList();

                    if (!approvePerson1.Equals("") && !approvePerson2.Equals("") && !approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            ld.ApprovePerson1Status = "Approved";
                            ld.ApprovePerson2Status = "Pending";
                            ld.ApprovePerson1 = Convert.ToInt64(approvePerson1);
                            ld.ApprovePerson2 = Convert.ToInt64(approvePerson2);
                            db.SaveChanges();

                            //InsertLeaveNotification(Convert.ToInt64(approvePerson2), ld.EmployeeId, false);
                            //SendEmailToAppliedEmp(leaveEmployeeId, Convert.ToInt64(approvePerson2), leaveDate);
                        }
                        else if (aprvPerson2Status == "Pending")
                        {
                            ld.ApprovePerson2Status = "Approved";
                            ld.ApprovePerson3Status = "Pending";
                            ld.ApprovePerson2 = Convert.ToInt64(approvePerson2);
                            ld.ApprovePerson3 = Convert.ToInt64(approvePerson3);
                            db.SaveChanges();

                            //InsertLeaveNotification(Convert.ToInt64(approvePerson3), ld.EmployeeId, false);
                            //SendEmailToAppliedEmp(leaveEmployeeId, Convert.ToInt64(approvePerson3), leaveDate);
                        }
                        else if (aprvPerson3Status == "Pending")
                        {
                            foreach (var levDet in leaveDetail)
                            {
                                
                                var ld1 = db.Lev_LeaveDetail.Where(x => x.LeaveDetailId == levDet.LeaveDetailId).FirstOrDefault();
                                ld1.LeaveStatus = "Approved";
                                db.SaveChanges();
                                SetLeaveCalculations(leaveEmployeeId, leaveTypeId, ld1.LeaveDate, (double)levDet.LeaveQty, leaveNo, CreatedUser, 0);

                            }

                            ld.Status = "Approved";
                            //SetLeaveCalculations(leaveEmployeeId, leaveTypeId, leaveDate, leaveQty, leaveNo, CreatedUser, DeductOT);
                            ld.ApprovePerson3Status = "Approved";
                            ld.ApprovePerson3 = Convert.ToInt64(approvePerson3);
                            ld.IsApprovedLeave = IsApprovedLeave;
                            db.SaveChanges();
                            
                        }
                    }
                    else if (!approvePerson1.Equals("") && !approvePerson2.Equals("") && approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            ld.ApprovePerson1Status = "Approved";
                            ld.ApprovePerson2Status = "Pending";
                            ld.ApprovePerson1 = Convert.ToInt64(approvePerson1);
                            ld.ApprovePerson2 = Convert.ToInt64(approvePerson2);
                            db.SaveChanges();
                            
                          
                            //InsertLeaveNotification(Convert.ToInt64(approvePerson2), ld.EmployeeId, false);
                            //SendEmailToAppliedEmp(leaveEmployeeId, Convert.ToInt64(approvePerson2), leaveDate);
                        }
                        if (aprvPerson2Status == "Pending")
                        {
                            foreach (var levDet in leaveDetail)
                            {
                                var ld1 = db.Lev_LeaveDetail.Where(x => x.LeaveDetailId == levDet.LeaveDetailId).FirstOrDefault();
                                ld1.LeaveStatus = "Approved";
                                db.SaveChanges();

                                SetLeaveCalculations(leaveEmployeeId, leaveTypeId, levDet.LeaveDate, (double)levDet.LeaveQty, leaveNo, CreatedUser, 0);
                            }

                            ld.Status = "Approved";
                            ld.ApprovePerson2Status = "Approved";
                            ld.ApprovePerson2 = Convert.ToInt64(approvePerson2);
                            ld.IsApprovedLeave = IsApprovedLeave;
                            db.SaveChanges();
                        }
                        /*if (aprvPerson1Status == "Approved" && aprvPerson2Status == "Pending")
                        {
                            //ld.ApprovePerson1Status = "Approved";
                            ld.ApprovePerson2Status = "Approved";
                            //ld.ApprovePerson1 = Convert.ToInt64(approvePerson1);
                            ld.ApprovePerson2 = Convert.ToInt64(approvePerson2);
                            db.SaveChanges();
                             
                                foreach (var levDet in leaveDetail)
                                {
                                    SetLeaveCalculations(leaveEmployeeId, leaveTypeId, levDet.LeaveDate, (double)levDet.LeaveQty, leaveNo, CreatedUser, levDet.DeductedOT);
                                    var ld1 = db.Lev_LeaveDetail.Where(x => x.LeaveDetailId == levDet.LeaveDetailId).FirstOrDefault();
                                    ld1.LeaveStatus = "Approved";
                                    db.SaveChanges();
                                }

                                ld.Status = "Approved";
                                ld.ApprovePerson2Status = "Approved";
                                ld.ApprovePerson2 = Convert.ToInt64(approvePerson2);
                                ld.IsApprovedLeave = IsApprovedLeave;
                                db.SaveChanges();
                        }*/
                        /*else if (aprvPerson1Status == "Pending")
                        {
                            foreach (var levDet in leaveDetail)
                            {
                                SetLeaveCalculations(leaveEmployeeId, leaveTypeId, levDet.LeaveDate, (double)levDet.LeaveQty, leaveNo, CreatedUser, levDet.DeductedOT);
                                var ld1 = db.Lev_LeaveDetail.Where(x => x.LeaveDetailId == levDet.LeaveDetailId).FirstOrDefault();
                                ld1.LeaveStatus = "Approved";
                                db.SaveChanges();
                            }

                            ld.Status = "Approved";
                            ld.ApprovePerson1Status = "Approved";
                            ld.ApprovePerson1 = Convert.ToInt64(approvePerson1);
                            ld.IsApprovedLeave = IsApprovedLeave;
                            db.SaveChanges();
                        }*/
                        /*else if (aprvPerson2Status == "Pending")
                        {
                            foreach (var levDet in leaveDetail)
                            {
                                SetLeaveCalculations(leaveEmployeeId, leaveTypeId, levDet.LeaveDate, (double)levDet.LeaveQty, leaveNo, CreatedUser, levDet.DeductedOT);
                                var ld1 = db.Lev_LeaveDetail.Where(x => x.LeaveDetailId == levDet.LeaveDetailId).FirstOrDefault();
                                ld1.LeaveStatus = "Approved";
                                db.SaveChanges();
                            }

                            ld.Status = "Approved";
                            ld.ApprovePerson2Status = "Approved";
                            ld.ApprovePerson2 = Convert.ToInt64(approvePerson2);
                            ld.IsApprovedLeave = IsApprovedLeave;
                            db.SaveChanges();
                        }*/
                    }
                    else if (!approvePerson1.Equals("") && approvePerson2.Equals("") && approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            foreach (var levDet in leaveDetail)
                            {
                                var ld1 = db.Lev_LeaveDetail.Where(x => x.LeaveDetailId == levDet.LeaveDetailId).FirstOrDefault();
                                ld1.LeaveStatus = "Approved";
                                db.SaveChanges();

                                SetLeaveCalculations(leaveEmployeeId, leaveTypeId, levDet.LeaveDate, (double)levDet.LeaveQty, leaveNo, CreatedUser, 0);
                            }

                            ld.Status = "Approved";
                            ld.ApprovePerson1Status = "Approved";
                            ld.ApprovePerson1 = Convert.ToInt64(approvePerson1);
                            ld.IsApprovedLeave = IsApprovedLeave;
                            db.SaveChanges();
                            InsertLeaveNotificationToAppliedEmp(leaveEmployeeId, Convert.ToInt64(approvePerson1), leaveDate);
                        }
                    }
                }

                //SendEmailToAppliedEmp(leaveEmployeeId, Convert.ToInt64(approvePerson1), leaveDate);
                result = true;

                return Json(new { success = result, message = "Submitted Successfully" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = result, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult ApprovePersonReject(int LeaveId, int EmployeeId, string RejectReason)
        {
            var result = false;
            try
            {
                long leaveEmployeeId = 0;
                string aprvPerson1Status = "";
                string aprvPerson2Status = "";
                string aprvPerson3Status = "";
                string approvePerson1 = "";
                string approvePerson2 = "";
                string approvePerson3 = "";
                DateTime leaveDate;
                int leaveTypeId;
                decimal leaveQty;

                var lev = db.Lev_LeaveHeader.Where(x => x.LeaveId == LeaveId).
                        Select(v => new
                        {
                            v.EmployeeId,
                            v.ApprovePerson1Status,
                            v.ApprovePerson2Status,
                            v.ApprovePerson3Status,
                            v.LeaveTypeId,
                        }
                        ).ToList();

                leaveEmployeeId = lev.Select(x => x.EmployeeId).SingleOrDefault();

                var levWF = db.Com_CommonApprovalWorkFlow.Join(
                        db.Com_CommonAssignData,
                        cap => cap.ApprovalWorkFlowId,
                        cas => cas.ApprovalWorkFlowId,
                        (cap, cas) => new { Com_CommonApprovalWorkFlow = cap, Com_CommonAssignData = cas }).Join(
                        db.ApprovalTypes,
                        cas => cas.Com_CommonAssignData.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cas, at) => new { cas.Com_CommonApprovalWorkFlow, cas.Com_CommonAssignData, ApprovalType = at }).Where(
                        x => x.ApprovalType.ApprovalTypeCode == "LA" && x.Com_CommonAssignData.EmployeeId == leaveEmployeeId).Select(v => new
                        {
                            v.Com_CommonApprovalWorkFlow.ApprovePerson1,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson2,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson3
                        }).ToList();

                leaveTypeId = lev.Select(x => x.LeaveTypeId).SingleOrDefault();
                aprvPerson1Status = lev.Select(x => x.ApprovePerson1Status).SingleOrDefault();
                aprvPerson2Status = lev.Select(x => x.ApprovePerson2Status).SingleOrDefault();
                aprvPerson3Status = lev.Select(x => x.ApprovePerson3Status).SingleOrDefault();
                approvePerson1 = levWF.Select(x => x.ApprovePerson1).FirstOrDefault().ToString();
                approvePerson2 = levWF.Select(x => x.ApprovePerson2).FirstOrDefault().ToString();
                approvePerson3 = levWF.Select(x => x.ApprovePerson3).FirstOrDefault().ToString();

                if (lev != null && levWF != null)
                {
                    var ld = db.Lev_LeaveHeader.Where(x => x.LeaveId == LeaveId).FirstOrDefault();

                    var leaveDetail = db.Lev_LeaveDetail.Where(x => x.LeaveId == LeaveId);
                    var date = db.Lev_LeaveDetail.Where(x => x.LeaveId == LeaveId).Select(x => x.LeaveDate).FirstOrDefault();
                    var ot = 0;
                    var leaveNumber = db.Lev_LeaveHeader.Where(x => x.LeaveId == LeaveId).Select(x => x.LeaveNo).FirstOrDefault();
                    var user = db.Lev_LeaveHeader.Where(x => x.LeaveId == LeaveId).Select(x => x.CreatedUser).FirstOrDefault();

                    if (!approvePerson1.Equals("") && !approvePerson2.Equals("") && !approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            foreach (var levDet in leaveDetail)
                            {
                                var ld1 = db.Lev_LeaveDetail.Where(x => x.LeaveDetailId == levDet.LeaveDetailId).FirstOrDefault();
                                ld1.LeaveStatus = "Rejected";
                                //db.SaveChanges();
                            }

                            ld.ApprovePerson1Status = "Rejected";
                            ld.Status = "Rejected";
                            ld.RejectedBy = EmployeeId;
                            ld.RejectedReason = RejectReason;
                            db.SaveChanges();
                            SetLeaveCalculations(leaveEmployeeId, leaveTypeId, date, (double)ld.TotalLeaveQty, leaveNumber, user, ot);
                            InsertLeaveNotificationWhenReject(EmployeeId, ld.EmployeeId, ld.FromDate, ld.Todate);
                            //SendEmailWhenLeaveRejected(LeaveId, EmployeeId);
                        }
                        else if (aprvPerson2Status == "Pending")
                        {
                            foreach (var levDet in leaveDetail)
                            {
                                var ld1 = db.Lev_LeaveDetail.Where(x => x.LeaveDetailId == levDet.LeaveDetailId).FirstOrDefault();
                                ld1.LeaveStatus = "Rejected";
                                //db.SaveChanges();
                            }

                            ld.ApprovePerson2Status = "Rejected";
                            ld.Status = "Rejected";
                            ld.RejectedBy = EmployeeId;
                            ld.RejectedReason = RejectReason;
                            db.SaveChanges();
                            SetLeaveCalculations(leaveEmployeeId, leaveTypeId, date, (double)ld.TotalLeaveQty, leaveNumber, user, ot);
                            InsertLeaveNotificationWhenReject(EmployeeId, ld.EmployeeId, ld.FromDate, ld.Todate);
                            //SendEmailWhenLeaveRejected(LeaveId, EmployeeId);
                        }
                        else if (aprvPerson3Status == "Pending")
                        {
                            foreach (var levDet in leaveDetail)
                            {
                                var ld1 = db.Lev_LeaveDetail.Where(x => x.LeaveDetailId == levDet.LeaveDetailId).FirstOrDefault();
                                ld1.LeaveStatus = "Rejected";
                                //db.SaveChanges();
                            }
                            ld.ApprovePerson3Status = "Rejected";
                            ld.Status = "Rejected";
                            ld.RejectedBy = EmployeeId;
                            ld.RejectedReason = RejectReason;
                            db.SaveChanges();
                            SetLeaveCalculations(leaveEmployeeId, leaveTypeId, date, (double)ld.TotalLeaveQty, leaveNumber, user, ot);
                            InsertLeaveNotificationWhenReject(EmployeeId, ld.EmployeeId, ld.FromDate, ld.Todate);
                            //SendEmailWhenLeaveRejected(LeaveId, EmployeeId);
                        }
                    }
                    else if (!approvePerson1.Equals("") && !approvePerson2.Equals("") && approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            foreach (var levDet in leaveDetail)
                            {
                                var ld1 = db.Lev_LeaveDetail.Where(x => x.LeaveDetailId == levDet.LeaveDetailId).FirstOrDefault();
                                ld1.LeaveStatus = "Rejected";
                                //db.SaveChanges();
                            }

                            ld.ApprovePerson1Status = "Rejected";
                            ld.Status = "Rejected";
                            ld.RejectedBy = EmployeeId;
                            ld.RejectedReason = RejectReason;
                            db.SaveChanges();
                            SetLeaveCalculations(leaveEmployeeId, leaveTypeId, date, (double)ld.TotalLeaveQty, leaveNumber, user, ot);
                            InsertLeaveNotificationWhenReject(EmployeeId, ld.EmployeeId, ld.FromDate, ld.Todate);
                            //SendEmailWhenLeaveRejected(LeaveId, EmployeeId);
                        }
                        else if (aprvPerson2Status == "Pending")
                        {
                            foreach (var levDet in leaveDetail)
                            {
                                var ld1 = db.Lev_LeaveDetail.Where(x => x.LeaveDetailId == levDet.LeaveDetailId).FirstOrDefault();
                                ld1.LeaveStatus = "Rejected";
                                //db.SaveChanges();
                            }

                            ld.ApprovePerson2Status = "Rejected";
                            ld.Status = "Rejected";
                            ld.RejectedBy = EmployeeId;
                            ld.RejectedReason = RejectReason;
                            db.SaveChanges();
                            SetLeaveCalculations(leaveEmployeeId, leaveTypeId, date, (double)ld.TotalLeaveQty, leaveNumber, user, ot);
                            InsertLeaveNotificationWhenReject(EmployeeId, ld.EmployeeId, ld.FromDate, ld.Todate);
                            //SendEmailWhenLeaveRejected(LeaveId, EmployeeId);
                        }
                    }
                    else if (!approvePerson1.Equals("") && approvePerson2.Equals("") && approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            foreach (var levDet in leaveDetail)
                            {
                                var ld1 = db.Lev_LeaveDetail.Where(x => x.LeaveDetailId == levDet.LeaveDetailId).FirstOrDefault();
                                ld1.LeaveStatus = "Rejected";
                                //db.SaveChanges();
                            }
                            ld.ApprovePerson1Status = "Rejected";
                            ld.Status = "Rejected";
                            ld.RejectedBy = EmployeeId;
                            ld.RejectedReason = RejectReason;
                            db.SaveChanges();
                            SetLeaveCalculations(leaveEmployeeId, leaveTypeId, date, (double)ld.TotalLeaveQty, leaveNumber, user, ot);
                            InsertLeaveNotificationWhenReject(EmployeeId, ld.EmployeeId, ld.FromDate, ld.Todate);
                            //SendEmailWhenLeaveRejected(LeaveId, EmployeeId);
                        }
                    }


                }

                result = true;

                return Json(new { success = result, message = "Submitted Successfully" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = result, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public ActionResult GetAbc(int EmployeeId)
        {
            try
            {
                db.Configuration.ProxyCreationEnabled = false;
                var newApLeaves = db.Lev_LeaveHeader.
                        Join(db.Lev_LeaveType,
                        ld => ld.LeaveTypeId,
                        lt => lt.LeaveTypeId,
                        (ld, lt) => new { Lev_LeaveHeader = ld, Lev_LeaveType = lt }).Join(
                        db.Com_CommonAssignData,
                        lh => lh.Lev_LeaveHeader.EmployeeId,
                        ca => ca.EmployeeId,
                        (ld, ca) => new { ld.Lev_LeaveHeader, ld.Lev_LeaveType, Com_CommonAssignData = ca }).Join(
                        db.Com_CommonApprovalWorkFlow,
                        ca => ca.Com_CommonAssignData.ApprovalWorkFlowId,
                        cw => cw.ApprovalWorkFlowId,
                        (ca, cw) => new { ca.Lev_LeaveHeader, ca.Lev_LeaveType, ca.Com_CommonAssignData, Com_CommonApprovalWorkFlow = cw }).Join(
                        db.ApprovalTypes,
                        cw => cw.Com_CommonApprovalWorkFlow.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cw, at) => new { cw.Lev_LeaveHeader, cw.Lev_LeaveType, cw.Com_CommonAssignData, cw.Com_CommonApprovalWorkFlow, ApprovalType = at }).Join(
                        db.Employees,
                        lh => lh.Lev_LeaveHeader.EmployeeId,
                        em => em.EmployeeId,
                        (lh, em) => new { lh.Lev_LeaveHeader, lh.Lev_LeaveType, lh.Com_CommonAssignData, lh.Com_CommonApprovalWorkFlow, lh.ApprovalType, Employee = em }).
                        Where(m => ((m.Com_CommonApprovalWorkFlow.ApprovePerson1 == EmployeeId && m.Lev_LeaveHeader.ApprovePerson1Status == "Approved") ||
                        (m.Com_CommonApprovalWorkFlow.ApprovePerson2 == EmployeeId && m.Lev_LeaveHeader.ApprovePerson2Status == "Approved") ||
                        (m.Com_CommonApprovalWorkFlow.ApprovePerson3 == EmployeeId && m.Lev_LeaveHeader.ApprovePerson3Status == "Approved")) &&
                        m.ApprovalType.ApprovalTypeCode == "LA" &&
                        m.Lev_LeaveHeader.IsApprovedByHR == false).ToList().GroupJoin(
                        db.Lev_LeaveReason,
                        lh => lh.Lev_LeaveHeader.LeaveReason,
                        lr => lr.LeaveReasonId,
                        (lh, lr) => new { lh.Lev_LeaveHeader, lh.Lev_LeaveType, lh.Employee, Lev_LeaveReason = lr }).ToList().SelectMany(m =>
                          m.Lev_LeaveReason.DefaultIfEmpty(null),
                        (x, y) => new
                        {
                            x.Lev_LeaveHeader.LeaveId,
                            EmployeeName = x.Employee.FirstName + " " + x.Employee.LastName,
                            FromDate = x.Lev_LeaveHeader.FromDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                            ToDate = x.Lev_LeaveHeader.Todate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                            LeaveQty = x.Lev_LeaveHeader.TotalLeaveQty,
                            x.Lev_LeaveHeader.EmployeeId,
                            Reason = (y == null || y.LeaveDescription == null ? "" : y.LeaveDescription),
                            x.Lev_LeaveType.LeaveDescription
                        }).ToList();

                return Json(new { data = newApLeaves }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { data = "" }, JsonRequestBehavior.AllowGet);
            }

        }

        [HttpGet]
        public ActionResult GetRejectedLeaves(int EmployeeId)
        {
            try
            {
                db.Configuration.ProxyCreationEnabled = false;
                var newApLeaves = db.Lev_LeaveHeader.
                        Join(db.Lev_LeaveType,
                        ld => ld.LeaveTypeId,
                        lt => lt.LeaveTypeId,
                        (ld, lt) => new { Lev_LeaveHeader = ld, Lev_LeaveType = lt }).Join(
                        db.Com_CommonAssignData,
                        lh => lh.Lev_LeaveHeader.EmployeeId,
                        ca => ca.EmployeeId,
                        (ld, ca) => new { ld.Lev_LeaveHeader, ld.Lev_LeaveType, Com_CommonAssignData = ca }).Join(
                        db.Com_CommonApprovalWorkFlow,
                        ca => ca.Com_CommonAssignData.ApprovalWorkFlowId,
                        cw => cw.ApprovalWorkFlowId,
                        (ca, cw) => new { ca.Lev_LeaveHeader, ca.Lev_LeaveType, ca.Com_CommonAssignData, Com_CommonApprovalWorkFlow = cw }).Join(
                        db.ApprovalTypes,
                        cw => cw.Com_CommonApprovalWorkFlow.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cw, at) => new { cw.Lev_LeaveHeader, cw.Lev_LeaveType, cw.Com_CommonAssignData, cw.Com_CommonApprovalWorkFlow, ApprovalType = at }).Join(
                        db.Employees,
                        lh => lh.Lev_LeaveHeader.EmployeeId,
                        em => em.EmployeeId,
                        (lh, em) => new { lh.Lev_LeaveHeader, lh.Lev_LeaveType, lh.Com_CommonAssignData, lh.Com_CommonApprovalWorkFlow, lh.ApprovalType, Employee = em }).
                        Where(m => ((m.Com_CommonApprovalWorkFlow.ApprovePerson1 == EmployeeId && m.Lev_LeaveHeader.ApprovePerson1Status == "Rejected") ||
                        (m.Com_CommonApprovalWorkFlow.ApprovePerson2 == EmployeeId && m.Lev_LeaveHeader.ApprovePerson2Status == "Rejected") ||
                        (m.Com_CommonApprovalWorkFlow.ApprovePerson3 == EmployeeId && m.Lev_LeaveHeader.ApprovePerson3Status == "Rejected")) &&
                        m.ApprovalType.ApprovalTypeCode == "LA" &&
                        m.Lev_LeaveHeader.IsApprovedByHR == false).ToList().GroupJoin(
                        db.Lev_LeaveReason,
                        lh => lh.Lev_LeaveHeader.LeaveReason,
                        lr => lr.LeaveReasonId,
                        (lh, lr) => new { lh.Lev_LeaveHeader, lh.Lev_LeaveType, lh.Employee, Lev_LeaveReason = lr }).ToList().SelectMany(m =>
                          m.Lev_LeaveReason.DefaultIfEmpty(null),
                        (x, y) => new
                        {
                            x.Lev_LeaveHeader.LeaveId,
                            EmployeeName = x.Employee.FirstName + " " + x.Employee.LastName,
                            FromDate = x.Lev_LeaveHeader.FromDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                            ToDate = x.Lev_LeaveHeader.Todate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                            LeaveQty = x.Lev_LeaveHeader.TotalLeaveQty,
                            x.Lev_LeaveHeader.EmployeeId,
                            Reason = (y == null || y.LeaveDescription == null ? "" : y.LeaveDescription),
                            x.Lev_LeaveType.LeaveDescription,
                            x.Lev_LeaveHeader.RejectedReason
                        }).ToList();

                return Json(new { data = newApLeaves }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { data = "" }, JsonRequestBehavior.AllowGet);
            }

        }

        [HttpGet]
        public ActionResult ViewLeaveDays(int LeaveId)
        {
            try
            {
                db.Configuration.ProxyCreationEnabled = false;
                var leaveDays = db.Lev_LeaveDetail.Where(x => x.LeaveId == LeaveId).ToList().Select(x => new
                {
                    LeaveDay = x.LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    x.Session,
                    x.LeaveQty
                }).ToList();

                return Json(new { data = leaveDays }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { data = "" }, JsonRequestBehavior.AllowGet);
            }

        }

/*        public ActionResult GetLeaveStatus(int LeaveId)
        {
            db.Configuration.ProxyCreationEnabled = false;

            List<ViewStatus> viewList = new List<ViewStatus>();

            var vacType = db.Lev_LeaveHeader.Where(x => x.LeaveId == LeaveId).ToList().Select(s => new
            {
                s.CoveringPerson,
                s.CoveringPersonStatus,
                s.RejectedBy,
                s.ApprovePerson1,
                s.ApprovePerson2,
                s.ApprovePerson3,
                s.RejectedReason,
                s.ApprovePerson1Status,
                s.ApprovePerson2Status,
                s.ApprovePerson3Status,
                s.IsApprovedByHR,
                s.Status,
                s.EmployeeId
            }).ToList();

            long? coveringPerson = vacType.Select(x => x.CoveringPerson).SingleOrDefault();
            long? approvePerson1 = vacType.Select(x => x.ApprovePerson1).SingleOrDefault();
            long? approvePerson2 = vacType.Select(x => x.ApprovePerson2).SingleOrDefault();
            long? approvePerson3 = vacType.Select(x => x.ApprovePerson3).SingleOrDefault();
            long? rejectedPerson = vacType.Select(x => x.RejectedBy).SingleOrDefault();
            string coveringPersonStatus = vacType.Select(x => x.CoveringPersonStatus).SingleOrDefault();
            string rejectedReason = vacType.Select(x => x.RejectedReason).SingleOrDefault();
            string approvePerson1Status = vacType.Select(x => x.ApprovePerson1Status).SingleOrDefault();
            string approvePerson2Status = vacType.Select(x => x.ApprovePerson2Status).SingleOrDefault();
            string approvePerson3Status = vacType.Select(x => x.ApprovePerson3Status).SingleOrDefault();
            bool isApprovedByHr = vacType.Select(x => x.IsApprovedByHR).SingleOrDefault();
            string status = vacType.Select(x => x.Status).SingleOrDefault();

            long leaveEmployeeId = vacType.Select(x => x.EmployeeId).SingleOrDefault();

            var levWF = db.Com_CommonApprovalWorkFlow.Join(
                        db.Com_CommonAssignData,
                        cap => cap.ApprovalWorkFlowId,
                        cas => cas.ApprovalWorkFlowId,
                        (cap, cas) => new { Com_CommonApprovalWorkFlow = cap, Com_CommonAssignData = cas }).Join(
                        db.ApprovalTypes,
                        cas => cas.Com_CommonAssignData.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cas, at) => new { cas.Com_CommonApprovalWorkFlow, cas.Com_CommonAssignData, ApprovalType = at }).Where(
                        x => x.ApprovalType.ApprovalTypeCode == "LA" && x.Com_CommonAssignData.EmployeeId == leaveEmployeeId).Select(v => new
                        {
                            v.Com_CommonApprovalWorkFlow.ApprovePerson1,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson2,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson3
                        }).FirstOrDefault();

            if (approvePerson1Status == "Pending")
            {
                approvePerson1 = levWF.ApprovePerson1;
            }
            if (approvePerson2Status == "Pending")
            {
                approvePerson2 = levWF.ApprovePerson2;
            }
            if (approvePerson3Status == "Pending")
            {
                approvePerson3 = levWF.ApprovePerson3;
            }

            if (isApprovedByHr)
            {
                viewList.Add(new ViewStatus { Level = "HR", Person = status + " By HR", Status = status, Remark = status == "Rejected" ? rejectedReason : "" });
            }
            else
            {
                if (coveringPerson != null)
                {
                    string approvedPersonName = db.Employees.Where(x => x.EmployeeId == coveringPerson).Select(x => x.EmployeeCode + "/" + x.NameWithInitial + " " + x.LastName).SingleOrDefault();
                    viewList.Add(new ViewStatus { Level = "Covering", Person = approvedPersonName, Status = coveringPersonStatus, Remark = coveringPersonStatus == "Rejected" ? rejectedReason : "" });
                }
                if (approvePerson1Status != null)
                {
                    string approvedPersonName = db.Employees.Where(x => x.EmployeeId == approvePerson1).Select(x => x.EmployeeCode + "/" + x.NameWithInitial + " " + x.LastName).SingleOrDefault();
                    viewList.Add(new ViewStatus { Level = "Level 1", Person = approvedPersonName, Status = approvePerson1Status, Remark = approvePerson1Status == "Rejected" ? rejectedReason : "" });
                }
                if (approvePerson2Status != null)
                {
                    string approvedPersonName = db.Employees.Where(x => x.EmployeeId == approvePerson2).Select(x => x.EmployeeCode + "/" + x.NameWithInitial + " " + x.LastName).SingleOrDefault();
                    viewList.Add(new ViewStatus { Level = "Level 2", Person = approvedPersonName, Status = approvePerson2Status, Remark = approvePerson2Status == "Rejected" ? rejectedReason : "" });
                }
                if (approvePerson3Status != null)
                {
                    string approvedPersonName = db.Employees.Where(x => x.EmployeeId == approvePerson3).Select(x => x.EmployeeCode + "/" + x.NameWithInitial + " " + x.LastName).SingleOrDefault();
                    viewList.Add(new ViewStatus { Level = "Level 3", Person = approvedPersonName, Status = approvePerson3Status, Remark = approvePerson3Status == "Rejected" ? rejectedReason : "" });
                }
            }
            return Json(new { data = viewList }, JsonRequestBehavior.AllowGet);

        }*/

        public ActionResult GetLeaveStatus(int LeaveId)
        {
            db.Configuration.ProxyCreationEnabled = false;

            List<ViewStatus> viewList = new List<ViewStatus>();

            var vacType = db.Lev_LeaveHeader.Where(x => x.LeaveId == LeaveId).ToList().Select(s => new
            {
                s.ApprovePerson1,
                s.ApprovePerson2,
                s.ApprovePerson3,
                s.ApprovePerson1Status,
                s.ApprovePerson2Status,
                s.ApprovePerson3Status,
                s.IsApprovedByHR,
                s.Status,
                s.RejectedReason
            }).ToList();

            long? approvePerson1 = vacType.Select(x => x.ApprovePerson1).SingleOrDefault();
            long? approvePerson2 = vacType.Select(x => x.ApprovePerson2).SingleOrDefault();
            long? approvePerson3 = vacType.Select(x => x.ApprovePerson3).SingleOrDefault();
            string approvePerson1Status = vacType.Select(x => x.ApprovePerson1Status).SingleOrDefault();
            string approvePerson2Status = vacType.Select(x => x.ApprovePerson2Status).SingleOrDefault();
            string approvePerson3Status = vacType.Select(x => x.ApprovePerson3Status).SingleOrDefault();
            string status = vacType.Select(x => x.Status).SingleOrDefault();
            bool isApprovedByHR = vacType.Select(x => x.IsApprovedByHR).SingleOrDefault();
            string rejReason = vacType.Select(x => x.RejectedReason).SingleOrDefault();
            if (isApprovedByHR && status == "Rejected")
            {
                viewList.Add(new ViewStatus { Level = "HR", Person = status + " By HR", Status = status, Remark = status == "Rejected" ? rejReason : "" });
            }
            else if (isApprovedByHR && status == "Approved")
            {
                viewList.Add(new ViewStatus { Level = "HR", Person = status + " By HR", Status = status, Remark = status == "Approved" ? rejReason : "" });
            }
            if (approvePerson1Status != null)
            {
                string approvedPersonName = db.Employees.Where(x => x.EmployeeId == approvePerson1).Select(x => x.EmployeeCode + "/" + x.NameWithInitial + " " + x.LastName).SingleOrDefault();
                viewList.Add(new ViewStatus { Level = "Level 1", Person = approvedPersonName, Status = approvePerson1Status, Remark = rejReason });
            }
            if (approvePerson2Status != null)
            {
                string approvedPersonName = db.Employees.Where(x => x.EmployeeId == approvePerson2).Select(x => x.EmployeeCode + "/" + x.NameWithInitial + " " + x.LastName).SingleOrDefault();
                viewList.Add(new ViewStatus { Level = "Level 2", Person = approvedPersonName, Status = approvePerson2Status, Remark = rejReason });
            }
            if (approvePerson3Status != null)
            {
                string approvedPersonName = db.Employees.Where(x => x.EmployeeId == approvePerson3).Select(x => x.EmployeeCode + "/" + x.NameWithInitial + " " + x.LastName).SingleOrDefault();
                viewList.Add(new ViewStatus { Level = "Level 3", Person = approvedPersonName, Status = approvePerson3Status, Remark = rejReason });
            }
            return Json(new { data = viewList }, JsonRequestBehavior.AllowGet);

        }

        public ActionResult GetLeaveQty(int EmployeeId, bool IsHalfDay, string FromDate, string ToDate, int LeaveTypeId)
        {
            double leaveQty = 0;
            bool result = false;

            try
            {
                DateTime fromDate = dateConvert.GetDateToAll(FromDate);
                DateTime toDate = dateConvert.GetDateToAll(ToDate);
                int companyId = (int)db.Employees.Where(x => x.EmployeeId == EmployeeId).Select(x => x.CompanyID).FirstOrDefault();
                var employeeShifts = db.Time_EmployeeShiftShedule.Where(x => x.EmployeeId == EmployeeId && x.ShiftDate >= fromDate && x.ShiftDate <= toDate).Join(
                        db.Time_ShiftSetup,
                        es => es.ShiftId,
                        ss => ss.ShiftId,
                        (es, ss) => new { Time_EmployeeShiftShedule = es, Time_ShiftSetup = ss }).Select(x => new {
                            x.Time_EmployeeShiftShedule.ShiftDate,
                            x.Time_ShiftSetup.AppliedLeaveQty
                        }).ToList();
                var calenderDates = db.Time_Calender.Where(x => x.CalendarDate >= fromDate && x.CalendarDate <= toDate && x.CompanyId == companyId).ToList();
                foreach (var dates in calenderDates)
                {
                    DateTime date = dates.CalendarDate;
                    int dayTypeId = dates.DayTypeId;
                    var shiftDates = employeeShifts.Where(x => x.ShiftDate == date).FirstOrDefault();
                    decimal shiftLeaveQty = employeeShifts.Where(x => x.ShiftDate == date).Select(x => x.AppliedLeaveQty).FirstOrDefault();
                    int dayOffLeaveTypeId = db.Lev_LeaveType.Where(x => x.LeaveCode == "DAYOFFFULL").Select(x => x.LeaveTypeId).SingleOrDefault();
                    int dayOffHalfLeaveTypeId = db.Lev_LeaveType.Where(x => x.LeaveCode == "DAYOFFHALF").Select(x => x.LeaveTypeId).SingleOrDefault();
                    if (shiftDates != null)
                    {
                        int shortLeaveTypeId = db.Lev_LeaveType.Where(x => x.LeaveCode == "SHORT").Select(x => x.LeaveTypeId).SingleOrDefault();
                        if (LeaveTypeId == shortLeaveTypeId)
                        {
                            leaveQty = leaveQty + 0.25;
                        }
                        else
                        {
                            if (IsHalfDay)
                            {
                                if (shiftLeaveQty ==2)
                                {
                                    leaveQty = leaveQty + 1;
                                }
                                else
                                {
                                    leaveQty = leaveQty + 0.5;
                                }
                                
                            }
                            else
                            {
                                var timeTxn = db.Time_EmployeeTxn.Where(x => x.EmployeeId == EmployeeId && x.Date == date).Select(x => new
                                {
                                    x.IsDayOff,
                                    x.DayOffLeaveQty
                                }).FirstOrDefault();
                                if ((timeTxn.IsDayOff == true && timeTxn.DayOffLeaveQty == 0.5M) || (timeTxn.IsDayOff == false && shiftLeaveQty == 0.5M))
                                {
                                    leaveQty = leaveQty + 0.5;
                                }
                                else if(shiftLeaveQty !=2)
                                {
                                    leaveQty = leaveQty + 1;
                                }
                                else
                                {
                                    leaveQty = leaveQty + 2;
                                }
                            }
                        }
                    }
                    else if (LeaveTypeId == dayOffLeaveTypeId || LeaveTypeId == dayOffHalfLeaveTypeId)
                    {
                        if (LeaveTypeId == dayOffLeaveTypeId)
                        {
                            leaveQty = leaveQty + 1;
                        }
                        else
                        {
                            if (IsHalfDay)
                            {
                                leaveQty = leaveQty + 0.5;
                            }
                        }
                    }
                    else
                    {
                        int shortLeaveTypeId = db.Lev_LeaveType.Where(x => x.LeaveCode == "SHORT").Select(x => x.LeaveTypeId).SingleOrDefault();
                        if (LeaveTypeId == shortLeaveTypeId)
                        {
                            leaveQty = leaveQty + 0.25;
                        }
                        else
                        {
                            if (IsHalfDay)
                            {
                                if (shiftLeaveQty == 2)
                                {
                                    leaveQty = leaveQty + 1;
                                }
                                else
                                {
                                    leaveQty = leaveQty + 0.5;
                                }

                            }
                            else
                            {
                                var timeTxn = db.Time_EmployeeTxn.Where(x => x.EmployeeId == EmployeeId && x.Date == date).Select(x => new
                                {
                                    x.IsDayOff,
                                    x.DayOffLeaveQty
                                }).FirstOrDefault();
                                if ((timeTxn.IsDayOff == true && timeTxn.DayOffLeaveQty == 0.5M) || (timeTxn.IsDayOff == false && shiftLeaveQty == 0.5M))
                                {
                                    leaveQty = leaveQty + 0.5;
                                }
                                else if (shiftLeaveQty != 2)
                                {
                                    leaveQty = leaveQty + 1;
                                }
                                else
                                {
                                    leaveQty = leaveQty + 2;
                                }
                            }
                        }
                    }
                }
                result = true;
                return Json(new { success = result, message = leaveQty }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = result, message = "0" }, JsonRequestBehavior.AllowGet);
            }
        }


        //-----------------------------------------------------------------------Leave Application-----------------------------------------------------------------------------------------------------------------------------------------
        public ActionResult LeaveApplication()
        {
            dynamic newModal = new ExpandoObject();
            //newModal.Employees = GetEmployeesDropDown();
            newModal.LeaveTypes = GetLeaveTypesDropDown();
            newModal.LeaveEmployees = GetLeaveEmployeesDropDown();
            newModal.HalfDayShifts = GetHalfDayShiftDropDown();
            newModal.LeaveReasons = GetLeaveResonDropDown();
            //newModal.Employees = GetLeaveEmployeesDropDownchange(0);
            return View(newModal);
        }

        //public List<Employee> GetLeaveEmployeesDropDownchange()
        //{
        //    int employeeId = 0;
        //    int companyId = Convert.ToInt32(Session["CompanyId"]);

        //    string userType = Session["UserTypeId"].ToString();

        //    var list = db.Database.SqlQuery<EmployeeDropdown>(
        //           "exec dbo.[POR_GetEmployeeforLevelTwoIndex] @EmpeeId,@CompanyId,@UserType",
        //            new Object[] {
        //            new SqlParameter("@EmpeeId", EmployeeId),
        //            new SqlParameter("@CompanyId", companyId),
        //            new SqlParameter("@UserType", userType),
        //            }).ToList();

        //    var empList = list.ToList().Select(x => new Employee
        //    {
        //        EmployeeId = x.EmployeeId,
        //        EmployeeCode = x.EmployeeCode,
        //        FirstName = x.FirstName,
        //        LastName = x.LastName
        //    }).ToList();

        //    return empList;
        //}

        public ActionResult GetLeaveCoveringPersonDropDown(DateTime fromDate, DateTime toDate)
        {
            int employeeId = 0;
            int companyId = Convert.ToInt32(Session["CompanyId"]);
            int DcId = Convert.ToInt32(Session["EmployeeId"]);

            if (Session["EmployeeId"] != null || Session["EmployeeId"] != "")
            {
                employeeId = Convert.ToInt32(Session["EmployeeId"]);
            }

            string userType = Session["UserTypeId"].ToString();

            var list = db.Database.SqlQuery<EmployeeDropdown>(
                "exec dbo.[COM_GetEmployeeListNew] @CompanyId,@CatID,@ActiveStatus,@EmpeeId",
                new Object[] {
            new SqlParameter("@CompanyId", companyId),
            new SqlParameter("@CatID", "0"),
            new SqlParameter("@ActiveStatus", 1),
            new SqlParameter("@EmpeeId", DcId)
                }).Select(x => new {
                    EmployeeIds = x.EmployeeId,
                    Name = x.FirstName
                }).Where(x => x.EmployeeIds != employeeId &&
                              !db.Lev_LeaveHeader.Any(l => l.EmployeeId == x.EmployeeIds &&
                                                           (l.FromDate <= toDate && l.Todate >= fromDate) && 
                                                           (l.FromDate == fromDate && l.Todate == fromDate))
                )
                .ToList();

            return Json(new { dataList = list }, JsonRequestBehavior.AllowGet);
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

        public ActionResult LeaveHistory()
        {
            dynamic newModal = new ExpandoObject();
            newModal.time = GetYears();
            newModal.Employees = GetLeaveEmployeesDropDown();
            // var yearsList = db.Time_Calender.GroupBy(x => x.Year).Select(x => x.Key).ToList().OrderByDescending(m => m).ToList();
            //var emp = db.Employees.GroupBy(x => x.EmployeeId).Select(x => x.Key).ToList().OrderByDescending(m => m).ToList(); ;
            //return View(emp);
            return View(newModal);
        }

        public List<year> GetYears()
        {
            int companyId = Convert.ToInt32(Session["CompanyId"]);
            var years = db.Database.SqlQuery<year>(
                              "exec dbo.[Com_Years] @CompanyId",
                              new SqlParameter("@CompanyId", companyId)
                          ).ToList();

            return years;
        }



        //public List<Time_Calender> GetYear()
        //{
        //    var yearsList = db.Time_Calender.GroupBy(x => x.Year).Select(x => x.Key).ToList().OrderByDescending(m => m).ToList();
        //    return yearsList;

        //}

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
                    db.Time_ShiftTypes.Where(x => x.CompanyId == companyId),
                    ss => ss.ShiftId,
                    st => st.ShiftId,
                    (ss, st) => new { Time_ShiftSetup = ss, Time_ShiftTypes = st }).Select(x => new HalfDayShift
                    {
                        ShiftId = x.Time_ShiftTypes.ShiftId,
                        ShiftName = x.Time_ShiftTypes.ShiftDescription
                    }).ToList();
            return shiftList;
        }

        public List<Lev_LeaveReason> GetLeaveResonDropDown()
        {
            var leaveReasonList = db.Lev_LeaveReason.ToList();
            return leaveReasonList;
        }
        //--------------------------------------------------------------------Leave Cancelation--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        [HttpPost]
        public JsonResult CancelLeave(int LeaveDetailId, string CancelReason, bool IsWorkFlow, string CreatedUser)
        {
            var result = false;
            try
            {
                var leave = db.Lev_LeaveDetail.Where(le => le.LeaveDetailId == LeaveDetailId).SingleOrDefault();
                if (leave != null)
                {
                    var checkLevCancel = db.Lev_CancelLeaves.Where(x => x.LeaveDetailId == LeaveDetailId && x.CancelStatus != "Rejected").FirstOrDefault();
                    if (checkLevCancel == null)
                    {
                        var leaveHeader = db.Lev_LeaveHeader.Where(x => x.LeaveId == leave.LeaveId).FirstOrDefault();

                        long leaveEmployeeId = leaveHeader.EmployeeId;

                        if (ValidPayroll.IsPostedPayperiod(leaveEmployeeId, leave.LeaveDate))
                        {

                        }
                        else
                        {
                            if (!IsWorkFlow)
                            {
                                // need to add leave cancelation calculation
                                leave.IsCancelWorkFlow = true;
                                db.SaveChanges();

                                LeaveCancelCalculation(leave.LeaveDetailId, leave.LeaveId, leaveEmployeeId, leave.LeaveDate, CreatedUser);
                            }
                            else
                            {
                                var levWF = db.Com_CommonApprovalWorkFlow.Join(
                                db.Com_CommonAssignData,
                                cap => cap.ApprovalWorkFlowId,
                                cas => cas.ApprovalWorkFlowId,
                                (cap, cas) => new { Com_CommonApprovalWorkFlow = cap, Com_CommonAssignData = cas }).Join(
                                db.ApprovalTypes,
                                cas => cas.Com_CommonAssignData.ApprovalTypeID,
                                at => at.ApprovalTypeID,
                                (cas, at) => new { cas.Com_CommonApprovalWorkFlow, cas.Com_CommonAssignData, ApprovalType = at }).Where(
                                x => x.ApprovalType.ApprovalTypeCode == "LA" && x.Com_CommonAssignData.EmployeeId == leaveEmployeeId).Select(v => new
                                {
                                    v.Com_CommonApprovalWorkFlow.ApprovePerson1,
                                    v.Com_CommonApprovalWorkFlow.ApprovePerson2,
                                    v.Com_CommonApprovalWorkFlow.ApprovePerson3
                                }).ToList();

                                if (levWF.Count <= 0 && IsWorkFlow)
                                {
                                    leaveMsg = "Need to assign for a leave work flow.";
                                    return Json(new { success = result, message = leaveMsg }, JsonRequestBehavior.AllowGet);
                                }
                                else
                                {
                                    var levCancel = new Lev_CancelLeaves();

                                    if (leaveHeader.ApprovePerson1Status == "Cancelled" || leaveHeader.Status == "Cancelled")
                                    {
                                        levCancel.IsCancelWorkFlow = true;
                                    }
                                    else
                                    {
                                        levCancel.IsCancelWorkFlow = false;
                                    }

                                    var checkLeaveHeaderHistory = db.Lev_LeaveHeaderHistory.Where(x => x.LeaveId == leave.LeaveId).FirstOrDefault();
                                    if (checkLeaveHeaderHistory == null)
                                    {
                                        var leaveHeaderHis = new Lev_LeaveHeaderHistory();
                                        leaveHeaderHis.HistoryCreatedUser = CreatedUser;
                                        leaveHeaderHis.HistoryCreatedDate = DateTime.Now;
                                        leaveHeaderHis.LeaveId = leaveHeader.LeaveId;
                                        leaveHeaderHis.EmployeeId = leaveHeader.EmployeeId;
                                        leaveHeaderHis.LeaveNo = leaveHeader.LeaveNo;
                                        leaveHeaderHis.FromDate = leaveHeader.FromDate;
                                        leaveHeaderHis.Todate = leaveHeader.Todate;
                                        leaveHeaderHis.LeaveTypeId = leaveHeader.LeaveTypeId;
                                        leaveHeaderHis.TotalLeaveQty = leaveHeader.TotalLeaveQty;
                                        leaveHeaderHis.Status = leaveHeader.Status;
                                        leaveHeaderHis.CoveringPerson = leaveHeader.CoveringPerson;
                                        leaveHeaderHis.IsApproved = leaveHeader.IsApproved;
                                        leaveHeaderHis.ApprovedBy = leaveHeader.ApprovedBy;
                                        leaveHeaderHis.ApprovedTotalQty = leaveHeader.ApprovedTotalQty;
                                        leaveHeaderHis.CreatedUser = leaveHeader.CreatedUser;
                                        leaveHeaderHis.CreatedDate = leaveHeader.CreatedDate;
                                        leaveHeaderHis.ModifiedUser = leaveHeader.ModifiedUser;
                                        leaveHeaderHis.ModifiedDate = leaveHeader.ModifiedDate;
                                        leaveHeaderHis.ApplicationName = leaveHeader.ApplicationName;
                                        leaveHeaderHis.IsCoveringApproved = leaveHeader.IsCoveringApproved;
                                        leaveHeaderHis.ISWorkFolw = leaveHeader.ISWorkFolw;
                                        leaveHeaderHis.ApprovePerson1 = leaveHeader.ApprovePerson1;
                                        leaveHeaderHis.ApprovePerson2 = leaveHeader.ApprovePerson2;
                                        leaveHeaderHis.ApprovePerson3 = leaveHeader.ApprovePerson3;
                                        leaveHeaderHis.ApprovePerson1Status = leaveHeader.ApprovePerson1Status;
                                        leaveHeaderHis.ApprovePerson2Status = leaveHeader.ApprovePerson2Status;
                                        leaveHeaderHis.ApprovePerson3Status = leaveHeader.ApprovePerson3Status;
                                        leaveHeaderHis.CoveringPersonStatus = leaveHeader.CoveringPersonStatus;
                                       // leaveHeaderHis.LeaveReason = leaveHeader.LeaveReason;
                                        leaveHeaderHis.RejectedReason = leaveHeader.RejectedReason;
                                        leaveHeaderHis.RejectedBy = leaveHeader.RejectedBy;
                                       // leaveHeaderHis.IsApprovedLeave = leaveHeader.IsApprovedLeave ?? false ;
                                        leaveHeaderHis.IsApprovedByHR = leaveHeader.IsApprovedByHR;
                                        db.Lev_LeaveHeaderHistory.Add(leaveHeaderHis);
                                        db.SaveChanges();
                                    }
                                    else
                                    {
                                        checkLeaveHeaderHistory.ModifiedUser = CreatedUser;
                                        checkLeaveHeaderHistory.ModifiedDate = DateTime.Now;
                                        checkLeaveHeaderHistory.FromDate = leaveHeader.FromDate;
                                        checkLeaveHeaderHistory.Todate = leaveHeader.Todate;
                                        checkLeaveHeaderHistory.TotalLeaveQty = leaveHeader.TotalLeaveQty;
                                        checkLeaveHeaderHistory.Status = leaveHeader.Status;
                                        checkLeaveHeaderHistory.CoveringPerson = leaveHeader.CoveringPerson;
                                        checkLeaveHeaderHistory.IsApproved = leaveHeader.IsApproved;
                                        checkLeaveHeaderHistory.ApprovedBy = leaveHeader.ApprovedBy;
                                        checkLeaveHeaderHistory.ApprovedTotalQty = leaveHeader.ApprovedTotalQty;
                                        checkLeaveHeaderHistory.ModifiedUser = leaveHeader.ModifiedUser;
                                        checkLeaveHeaderHistory.ModifiedDate = leaveHeader.ModifiedDate;
                                        checkLeaveHeaderHistory.ApplicationName = leaveHeader.ApplicationName;
                                        checkLeaveHeaderHistory.IsCoveringApproved = leaveHeader.IsCoveringApproved;
                                        checkLeaveHeaderHistory.ISWorkFolw = leaveHeader.ISWorkFolw;
                                        checkLeaveHeaderHistory.ApprovePerson1 = leaveHeader.ApprovePerson1;
                                        checkLeaveHeaderHistory.ApprovePerson2 = leaveHeader.ApprovePerson2;
                                        checkLeaveHeaderHistory.ApprovePerson3 = leaveHeader.ApprovePerson3;
                                        checkLeaveHeaderHistory.ApprovePerson1Status = leaveHeader.ApprovePerson1Status;
                                        checkLeaveHeaderHistory.ApprovePerson2Status = leaveHeader.ApprovePerson2Status;
                                        checkLeaveHeaderHistory.ApprovePerson3Status = leaveHeader.ApprovePerson3Status;
                                        checkLeaveHeaderHistory.CoveringPersonStatus = leaveHeader.CoveringPersonStatus;
                                       // checkLeaveHeaderHistory.LeaveReason = leaveHeader.LeaveReason;
                                        checkLeaveHeaderHistory.RejectedReason = leaveHeader.RejectedReason;
                                        checkLeaveHeaderHistory.RejectedBy = leaveHeader.RejectedBy;
                                      //  checkLeaveHeaderHistory.IsApprovedLeave = leaveHeader.IsApprovedLeave ?? false ;
                                        checkLeaveHeaderHistory.IsApprovedByHR = leaveHeader.IsApprovedByHR;
                                        db.SaveChanges();
                                    }

                                    long? approvePerson1 = levWF.Select(x => x.ApprovePerson1).FirstOrDefault();


                                    levCancel.HistoryCreatedUser = CreatedUser;
                                    levCancel.HistoryCreatedDate = DateTime.Now;
                                    levCancel.LeaveDetailId = leave.LeaveDetailId;
                                    levCancel.LeaveId = leave.LeaveId;
                                    levCancel.LeaveTypeId = leave.LeaveTypeId;
                                    levCancel.LeaveDate = leave.LeaveDate;
                                    levCancel.LeaveQty = leave.LeaveQty;
                                    levCancel.Session = leave.Session;
                                    levCancel.DayTypeId = leave.DayTypeId;
                                    levCancel.ShiftId = leave.ShiftId;
                                    levCancel.DateName = leave.DateName;
                                    levCancel.MedicalNo = leave.MedicalNo;
                                    levCancel.Approved = leave.Approved;
                                    levCancel.ApprovedQty = leave.ApprovedQty;
                                    levCancel.IsActive = leave.IsActive;
                                    levCancel.IsNoPay = leave.IsNoPay;
                                    levCancel.LeaveStatus = leave.LeaveStatus;
                                    levCancel.CoveringPersonStatus = leave.CoveringPersonStatus;
                                    levCancel.Reason = leave.Reason;
                                    levCancel.CancelReason = CancelReason;
                                  //  levCancel.DeductedOT = leave.DeductedOT ?? 0 ;
                                    levCancel.CoveredDay = leave.CoveredDay;

                                    if (levCancel.IsCancelWorkFlow == true)
                                    {
                                        levCancel.CancelApprovePerson1 = approvePerson1;
                                        levCancel.CancelApprovePerson1Status = "Pending";
                                        levCancel.CancelStatus = "Pending";

                                        leave.IsCancelWorkFlow = true;
                                        db.SaveChanges();

                                        //InsertLeaveNotificationWhenLeaveCancelApprove((long)approvePerson1, leaveEmployeeId);
                                        //SendEmailToLeaveCancelApprovePerson(LeaveDetailId, (long)approvePerson1);
                                    }
                                    else
                                    {
                                        levCancel.CancelStatus = "Cancelled";
                                    }

                                    db.Lev_CancelLeaves.Add(levCancel);
                                    db.SaveChanges();

                                    if (levCancel.IsCancelWorkFlow == false)
                                    {
                                        LeaveCancelCalculation(leave.LeaveDetailId, leave.LeaveId, leaveEmployeeId, leave.LeaveDate, CreatedUser);
                                    }

                                    result = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        return Json(new { success = result, message = "Record aleready in cancellation process." }, JsonRequestBehavior.AllowGet);
                    }

                    return Json(new { success = result, message = "Submitted Successfully" }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = result, message = "Submitted Failed" }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = result, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public void InsertLeaveNotificationWhenLeaveCancelApprove(long EmployeeId, long LeaveEmployeeId)
        {
            string content = "";
            string functionName = "";
            var leaveEmployeeName = db.Employees.Where(x => x.EmployeeId == LeaveEmployeeId).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault();
            content = leaveEmployeeName + " has requested for cancel a leave.";
            functionName = "LeaveCancelApproval";

            Por_Notification noti = new Por_Notification();
            noti.EmployeeId = EmployeeId;
            noti.NotificationTypeId = 2;
            noti.Content = content;
            noti.ControllerName = "Leave";
            noti.FunctionName = functionName;
            noti.IsExpired = false;
            noti.CreatedDate = DateTime.Now;
            db.Por_Notification.Add(noti);
            db.SaveChanges();
        }

        public void SendEmailToLeaveCancelApprovePerson(long LeaveCanelId, long ApprovePersonId)
        {
            StringWriter writer = new StringWriter();
            HtmlTextWriter html = new HtmlTextWriter(writer);
            string senderEmail = ConfigurationManager.AppSettings["LeaveEmail"];

            var leaveDetail = db.Lev_CancelLeaves.Where(x => x.LeaveDetailId == LeaveCanelId).Join(
                        db.Lev_LeaveHeaderHistory,
                        ld => ld.LeaveId,
                        lh => lh.LeaveId,
                        (ld, lh) => new { Lev_LeaveDetail = ld, Lev_LeaveHeader = lh }).Select(x => new
                        {
                            x.Lev_LeaveHeader.EmployeeId,
                            x.Lev_LeaveDetail.LeaveDate,
                        }).FirstOrDefault();

            if (leaveDetail != null)
            {
                var leaveEmployeeName = db.Employees.Where(x => x.EmployeeId == leaveDetail.EmployeeId).Join(
                            db.CompanyProfiles,
                            em => em.CompanyID,
                            co => co.CompanyID,
                            (em, co) => new { Employee = em, CompanyProfile = co }).Select(x =>
                             x.CompanyProfile.EmployeeReportViewName == 1 ? x.Employee.EmployeeCode + " | " + x.Employee.FirstName :
                             x.CompanyProfile.EmployeeReportViewName == 2 ? x.Employee.EmployeeCode + " | " + x.Employee.LastName :
                             x.CompanyProfile.EmployeeReportViewName == 3 ? x.Employee.EmployeeCode + " | " + x.Employee.CallName :
                             x.CompanyProfile.EmployeeReportViewName == 4 ? x.Employee.EmployeeCode + " | " + x.Employee.NameWithInitial + " " + x.Employee.LastName :
                             x.CompanyProfile.EmployeeReportViewName == 5 ? x.Employee.EPFNo + " | " + x.Employee.FirstName :
                             x.CompanyProfile.EmployeeReportViewName == 6 ? x.Employee.EPFNo + " | " + x.Employee.LastName :
                             x.CompanyProfile.EmployeeReportViewName == 7 ? x.Employee.EPFNo + " | " + x.Employee.CallName :
                             x.Employee.EPFNo + " | " + x.Employee.NameWithInitial + " " + x.Employee.LastName).FirstOrDefault();
                var approveEmail = db.Employees.Where(x => x.EmployeeId == ApprovePersonId).Select(x => new
                {
                    x.Email,
                    ApprovePersonName = x.FirstName + " " + x.LastName
                }).FirstOrDefault();

                Style style = new Style();
                style.Font.Name = "Verdana";
                style.Font.Size = 10;
                style.Font.Bold = false;
                html.EnterStyle(style);
                html.RenderBeginTag(HtmlTextWriterTag.P);
                html.WriteEncodedText(string.Format("Dear {0},", approveEmail.ApprovePersonName));
                html.WriteBreak();
                html.RenderBeginTag(HtmlTextWriterTag.P);

                html.WriteEncodedText(string.Format("{0} has requested for cancel a leave that applied on {1}.", leaveEmployeeName, leaveDetail.LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
                html.WriteEncodedText(" Please log into the Employee Self Service Portal to accept the request.");
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
                html.WriteEncodedText("Click Here to Log ESS Portal: <a href='https://hrm.sportingstar.lk:3065/'>https://hrm.sportingstar.lk:3065/</a>");
                html.WriteBreak();
                html.WriteEncodedText("Please note that this email is automatically generated from the system, therefore, do not need to reply.");
                html.WriteBreak();
                html.Flush();
                string htmlString = writer.ToString();
                string subject = "Leave Approve Request from " + leaveEmployeeName;

                CRUD_OperationByMeUsingJqueryAjaxMvc.Email.Email email = new CRUD_OperationByMeUsingJqueryAjaxMvc.Email.Email();

                if (approveEmail.Email != "" && approveEmail.Email != null)
                    email.SendemailInExchangeServer(senderEmail, approveEmail.Email, subject, htmlString,GetCCList());
                //email.SendemailInExchangeServer("isharaz2810@gmail.com", "ishara@infoxglobal.com", subject, htmlString, "");
            }
        }

        public void SendEmailWhenLeaveCancelationRejected(long LeaveCanelId, long ApprovePersonId)
        {
            StringWriter writer = new StringWriter();
            HtmlTextWriter html = new HtmlTextWriter(writer);
            string senderEmail = ConfigurationManager.AppSettings["LeaveEmail"];

            var leaveDetail = db.Lev_CancelLeaves.Where(x => x.LeaveDetailId == LeaveCanelId).Join(
                        db.Lev_LeaveHeaderHistory,
                        ld => ld.LeaveId,
                        lh => lh.LeaveId,
                        (ld, lh) => new { Lev_LeaveDetail = ld, Lev_LeaveHeader = lh }).Select(x => new
                        {
                            x.Lev_LeaveHeader.EmployeeId,
                            x.Lev_LeaveDetail.LeaveDate,
                        }).FirstOrDefault();

            if (leaveDetail != null)
            {
                var leaveEmployeeName = db.Employees.Where(x => x.EmployeeId == leaveDetail.EmployeeId).Select(x => new
                {
                    LeaveEmployeeName = x.FirstName + " " + x.LastName,
                    x.Email
                }).FirstOrDefault();

                var approveEmail = db.Employees.Where(x => x.EmployeeId == ApprovePersonId).Select(x => new
                {
                    x.Email,
                    ApprovePersonName = x.FirstName + " " + x.LastName
                }).FirstOrDefault();

                Style style = new Style();
                style.Font.Name = "Verdana";
                style.Font.Size = 10;
                style.Font.Bold = false;
                html.EnterStyle(style);
                html.RenderBeginTag(HtmlTextWriterTag.P);
                html.WriteEncodedText(string.Format("Dear {0},", leaveEmployeeName.LeaveEmployeeName));
                html.WriteBreak();
                html.RenderBeginTag(HtmlTextWriterTag.P);

                html.WriteEncodedText(string.Format("On {0} leave cancellation that you requested have rejected by {1}.", leaveDetail.LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), approveEmail.ApprovePersonName));
                html.WriteEncodedText(" Please log into the Employee Self Service Portal to get more details.");
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
                html.WriteEncodedText("Click Here to Log ESS Portal: <a href='https://hrm.sportingstar.lk:3065/'>https://hrm.sportingstar.lk:3065/</a>");
                html.WriteBreak();
                html.WriteEncodedText("Please note that this email is automatically generated from the system, therefore, do not need to reply.");
                html.WriteBreak();
                html.Flush();
                string htmlString = writer.ToString();
                string subject = "Leave Approve Request from " + leaveEmployeeName;

                CRUD_OperationByMeUsingJqueryAjaxMvc.Email.Email email = new CRUD_OperationByMeUsingJqueryAjaxMvc.Email.Email();

                if (leaveEmployeeName.Email != "" && leaveEmployeeName.Email != null)
                    email.SendemailInExchangeServer(senderEmail, leaveEmployeeName.Email, subject, htmlString, GetCCList());
                //email.SendemailInExchangeServer("isharaz2810@gmail.com", "ishara@infoxglobal.com", subject, htmlString, "");
            }
        }

        public void LeaveCancelCalculation(long LeaveDetailId, long LeaveId, long EmployeeId, DateTime LeaveDate, string CreatedUser)
        {
            var affectedRows = db.Database.ExecuteSqlCommand("Lev_CancelLeaveByESSP @LeaveDetailId,@LeaveId,@EmployeeId,@CreateUser,@CancelledByHr",
                        new SqlParameter("@LeaveDetailId", LeaveDetailId),
                        new SqlParameter("@LeaveId", LeaveId),
                        new SqlParameter("@EmployeeId", EmployeeId),
                        new SqlParameter("@CreateUser", CreatedUser),
                        new SqlParameter("@CancelledByHr", false));

            var objTimeTxn = db.Time_EmployeeTxn.Where(x => x.EmployeeId == EmployeeId && x.Date == LeaveDate).FirstOrDefault();
            TimeReCalculate(objTimeTxn, CreatedUser);
        }

        public ActionResult GetLeaveCancelStatus(int LeaveCancelId)
        {
            db.Configuration.ProxyCreationEnabled = false;

            List<ViewStatus> viewList = new List<ViewStatus>();

            var vacType = db.Lev_CancelLeaves.Where(x => x.LeaveCancelId == LeaveCancelId).ToList().Select(s => new
            {
                s.LeaveId,
                s.CancelRejectPerson,
                s.CancelApprovePerson1,
                s.CancelApprovePerson2,
                s.CancelApprovePerson3,
                s.CancelRejectReason,
                s.CancelApprovePerson1Status,
                s.CancelApprovePerson2Status,
                s.CancelApprovePerson3Status,
                s.IsCancelledByHR,
                s.CancelStatus
            }).ToList();

            long leaveId = vacType.Select(x => x.LeaveId).SingleOrDefault();
            long? approvePerson1 = vacType.Select(x => x.CancelApprovePerson1).SingleOrDefault();
            long? approvePerson2 = vacType.Select(x => x.CancelApprovePerson2).SingleOrDefault();
            long? approvePerson3 = vacType.Select(x => x.CancelApprovePerson3).SingleOrDefault();
            long? rejectedPerson = vacType.Select(x => x.CancelRejectPerson).SingleOrDefault();
            string rejectedReason = vacType.Select(x => x.CancelRejectReason).SingleOrDefault();
            string approvePerson1Status = vacType.Select(x => x.CancelApprovePerson1Status).SingleOrDefault();
            string approvePerson2Status = vacType.Select(x => x.CancelApprovePerson2Status).SingleOrDefault();
            string approvePerson3Status = vacType.Select(x => x.CancelApprovePerson3Status).SingleOrDefault();
            bool isCancelledByHR = vacType.Select(x => x.IsCancelledByHR).SingleOrDefault();
            string cancelStatus = vacType.Select(x => x.CancelStatus).SingleOrDefault();

            long leaveEmployeeId = db.Lev_LeaveHeaderHistory.Where(x => x.LeaveId == leaveId).Select(x => x.EmployeeId).FirstOrDefault();

            var levWF = db.Com_CommonApprovalWorkFlow.Join(
                        db.Com_CommonAssignData,
                        cap => cap.ApprovalWorkFlowId,
                        cas => cas.ApprovalWorkFlowId,
                        (cap, cas) => new { Com_CommonApprovalWorkFlow = cap, Com_CommonAssignData = cas }).Join(
                        db.ApprovalTypes,
                        cas => cas.Com_CommonAssignData.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cas, at) => new { cas.Com_CommonApprovalWorkFlow, cas.Com_CommonAssignData, ApprovalType = at }).Where(
                        x => x.ApprovalType.ApprovalTypeCode == "LA" && x.Com_CommonAssignData.EmployeeId == leaveEmployeeId).Select(v => new
                        {
                            v.Com_CommonApprovalWorkFlow.ApprovePerson1,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson2,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson3
                        }).FirstOrDefault();

            if (approvePerson1Status == "Pending")
            {
                approvePerson1 = levWF.ApprovePerson1;
            }
            if (approvePerson2Status == "Pending")
            {
                approvePerson2 = levWF.ApprovePerson2;
            }
            if (approvePerson3Status == "Pending")
            {
                approvePerson3 = levWF.ApprovePerson3;
            }

            if (isCancelledByHR)
            {
                viewList.Add(new ViewStatus { Level = "HR", Person = "Cancelled By HR", Status = cancelStatus, Remark = cancelStatus == "Rejected" ? rejectedReason : "" });
            }
            else
            {
                if (approvePerson1Status != null)
                {
                    string approvedPersonName = db.Employees.Where(x => x.EmployeeId == approvePerson1).Select(x => x.EmployeeCode + "/" + x.NameWithInitial + " " + x.LastName).SingleOrDefault();
                    viewList.Add(new ViewStatus { Level = "Level 1", Person = approvedPersonName, Status = approvePerson1Status, Remark = approvePerson1Status == "Rejected" ? rejectedReason : "" });
                }
                if (approvePerson2Status != null)
                {
                    string approvedPersonName = db.Employees.Where(x => x.EmployeeId == approvePerson2).Select(x => x.EmployeeCode + "/" + x.NameWithInitial + " " + x.LastName).SingleOrDefault();
                    viewList.Add(new ViewStatus { Level = "Level 2", Person = approvedPersonName, Status = approvePerson2Status, Remark = approvePerson2Status == "Rejected" ? rejectedReason : "" });
                }
                if (approvePerson3Status != null)
                {
                    string approvedPersonName = db.Employees.Where(x => x.EmployeeId == approvePerson3).Select(x => x.EmployeeCode + "/" + x.NameWithInitial + " " + x.LastName).SingleOrDefault();
                    viewList.Add(new ViewStatus { Level = "Level 3", Person = approvedPersonName, Status = approvePerson3Status, Remark = approvePerson3Status == "Rejected" ? rejectedReason : "" });
                }
            }
            return Json(new { data = viewList }, JsonRequestBehavior.AllowGet);

        }

        //-------------------------------------------------------------------------Leave Cancel Approval------------------------------------------------------------------------------------------------------------------------------------------------
        public ActionResult LeaveCancelApproval()
        {
            //if (Session["EmployeeId"] != null)
            //{
            //    uscObj.CheckPermission("LeaveCancelApproval", Convert.ToInt64(Session["EmployeeId"]));
            //}
            return View();
        }

        public ActionResult GetLeaveCancelApproval(int EmployeeId)
        {
            db.Configuration.ProxyCreationEnabled = false;
            var leaveApproval =
                        db.Lev_LeaveHeaderHistory.Join(
                        db.Lev_CancelLeaves,
                        lh => lh.LeaveId,
                        ld => ld.LeaveId,
                        (lh, ld) => new { Lev_LeaveHeaderHistory = lh, Lev_CancelLeaves = ld }).Join(
                        db.Lev_LeaveType,
                        lh => lh.Lev_CancelLeaves.LeaveTypeId,
                        lt => lt.LeaveTypeId,
                        (ld, lt) => new { ld.Lev_LeaveHeaderHistory, ld.Lev_CancelLeaves, Lev_LeaveType = lt }).Join(
                        db.Com_CommonAssignData,
                        lh => lh.Lev_LeaveHeaderHistory.EmployeeId,
                        ca => ca.EmployeeId,
                        (ld, ca) => new { ld.Lev_LeaveHeaderHistory, ld.Lev_CancelLeaves, ld.Lev_LeaveType, Com_CommonAssignData = ca }).Join(
                        db.Com_CommonApprovalWorkFlow,
                        ca => ca.Com_CommonAssignData.ApprovalWorkFlowId,
                        cw => cw.ApprovalWorkFlowId,
                        (ca, cw) => new { ca.Lev_LeaveHeaderHistory, ca.Lev_CancelLeaves, ca.Lev_LeaveType, ca.Com_CommonAssignData, Com_CommonApprovalWorkFlow = cw }).Join(
                        db.ApprovalTypes,
                        cw => cw.Com_CommonApprovalWorkFlow.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cw, at) => new { cw.Lev_LeaveHeaderHistory, cw.Lev_CancelLeaves, cw.Lev_LeaveType, cw.Com_CommonAssignData, cw.Com_CommonApprovalWorkFlow, ApprovalType = at }).Join(
                        db.Employees,
                        lh => lh.Lev_LeaveHeaderHistory.EmployeeId,
                        em => em.EmployeeId,
                        (lh, em) => new { lh.Lev_LeaveHeaderHistory, lh.Lev_CancelLeaves, lh.Lev_LeaveType, lh.Com_CommonAssignData, lh.Com_CommonApprovalWorkFlow, lh.ApprovalType, EmpName = em.FirstName + " " + em.LastName }).
                        Where(m => ((m.Com_CommonApprovalWorkFlow.ApprovePerson1 == EmployeeId && m.Lev_CancelLeaves.CancelApprovePerson1Status == "Pending") ||
                        (m.Com_CommonApprovalWorkFlow.ApprovePerson2 == EmployeeId && m.Lev_CancelLeaves.CancelApprovePerson2Status == "Pending") ||
                        (m.Com_CommonApprovalWorkFlow.ApprovePerson3 == EmployeeId && m.Lev_CancelLeaves.CancelApprovePerson3Status == "Pending")) &&
                        m.ApprovalType.ApprovalTypeCode == "LA").ToList().Select(v => new
                        {
                            v.Lev_LeaveHeaderHistory.LeaveId,
                            v.Lev_CancelLeaves.LeaveDetailId,
                            EmployeeName = v.EmpName,
                            LeaveDate = v.Lev_CancelLeaves.LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                            v.Lev_CancelLeaves.LeaveQty,
                            v.Lev_LeaveHeaderHistory.EmployeeId,
                            Reason = v.Lev_CancelLeaves.CancelReason,
                            v.Lev_LeaveType.LeaveDescription,
                            v.Lev_CancelLeaves.LeaveCancelId,
                        }).ToList();

            return Json(new { data = leaveApproval }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetApprovedLeaveCancelApproval(int EmployeeId)
        {
            db.Configuration.ProxyCreationEnabled = false;
            var leaveApproval =
                        db.Lev_LeaveHeaderHistory.Join(
                        db.Lev_CancelLeaves,
                        lh => lh.LeaveId,
                        ld => ld.LeaveId,
                        (lh, ld) => new { Lev_LeaveHeaderHistory = lh, Lev_CancelLeaves = ld }).Join(
                        db.Lev_LeaveType,
                        lh => lh.Lev_CancelLeaves.LeaveTypeId,
                        lt => lt.LeaveTypeId,
                        (ld, lt) => new { ld.Lev_LeaveHeaderHistory, ld.Lev_CancelLeaves, Lev_LeaveType = lt }).Join(
                        db.Com_CommonAssignData,
                        lh => lh.Lev_LeaveHeaderHistory.EmployeeId,
                        ca => ca.EmployeeId,
                        (ld, ca) => new { ld.Lev_LeaveHeaderHistory, ld.Lev_CancelLeaves, ld.Lev_LeaveType, Com_CommonAssignData = ca }).Join(
                        db.Com_CommonApprovalWorkFlow,
                        ca => ca.Com_CommonAssignData.ApprovalWorkFlowId,
                        cw => cw.ApprovalWorkFlowId,
                        (ca, cw) => new { ca.Lev_LeaveHeaderHistory, ca.Lev_CancelLeaves, ca.Lev_LeaveType, ca.Com_CommonAssignData, Com_CommonApprovalWorkFlow = cw }).Join(
                        db.ApprovalTypes,
                        cw => cw.Com_CommonApprovalWorkFlow.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cw, at) => new { cw.Lev_LeaveHeaderHistory, cw.Lev_CancelLeaves, cw.Lev_LeaveType, cw.Com_CommonAssignData, cw.Com_CommonApprovalWorkFlow, ApprovalType = at }).Join(
                        db.Employees,
                        lh => lh.Lev_LeaveHeaderHistory.EmployeeId,
                        em => em.EmployeeId,
                        (lh, em) => new { lh.Lev_LeaveHeaderHistory, lh.Lev_CancelLeaves, lh.Lev_LeaveType, lh.Com_CommonAssignData, lh.Com_CommonApprovalWorkFlow, lh.ApprovalType, EmpName = em.FirstName + " " + em.LastName }).
                        Where(m => ((m.Com_CommonApprovalWorkFlow.ApprovePerson1 == EmployeeId && m.Lev_CancelLeaves.CancelApprovePerson1Status == "Approved") ||
                        (m.Com_CommonApprovalWorkFlow.ApprovePerson2 == EmployeeId && m.Lev_CancelLeaves.CancelApprovePerson2Status == "Approved") ||
                        (m.Com_CommonApprovalWorkFlow.ApprovePerson3 == EmployeeId && m.Lev_CancelLeaves.CancelApprovePerson3Status == "Approved")) &&
                        m.ApprovalType.ApprovalTypeCode == "LA").ToList().Select(v => new
                        {
                            v.Lev_LeaveHeaderHistory.LeaveId,
                            v.Lev_CancelLeaves.LeaveDetailId,
                            EmployeeName = v.EmpName,
                            LeaveDate = v.Lev_CancelLeaves.LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                            v.Lev_CancelLeaves.LeaveQty,
                            v.Lev_LeaveHeaderHistory.EmployeeId,
                            Reason = v.Lev_CancelLeaves.CancelReason,
                            v.Lev_LeaveType.LeaveDescription,
                            v.Lev_CancelLeaves.LeaveCancelId,
                        }).ToList();

            return Json(new { data = leaveApproval }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetRejectedLeaveCancelApproval(int EmployeeId)
        {
            db.Configuration.ProxyCreationEnabled = false;
            var leaveApproval =
                        db.Lev_LeaveHeaderHistory.Join(
                        db.Lev_CancelLeaves,
                        lh => lh.LeaveId,
                        ld => ld.LeaveId,
                        (lh, ld) => new { Lev_LeaveHeaderHistory = lh, Lev_CancelLeaves = ld }).Join(
                        db.Lev_LeaveType,
                        lh => lh.Lev_CancelLeaves.LeaveTypeId,
                        lt => lt.LeaveTypeId,
                        (ld, lt) => new { ld.Lev_LeaveHeaderHistory, ld.Lev_CancelLeaves, Lev_LeaveType = lt }).Join(
                        db.Com_CommonAssignData,
                        lh => lh.Lev_LeaveHeaderHistory.EmployeeId,
                        ca => ca.EmployeeId,
                        (ld, ca) => new { ld.Lev_LeaveHeaderHistory, ld.Lev_CancelLeaves, ld.Lev_LeaveType, Com_CommonAssignData = ca }).Join(
                        db.Com_CommonApprovalWorkFlow,
                        ca => ca.Com_CommonAssignData.ApprovalWorkFlowId,
                        cw => cw.ApprovalWorkFlowId,
                        (ca, cw) => new { ca.Lev_LeaveHeaderHistory, ca.Lev_CancelLeaves, ca.Lev_LeaveType, ca.Com_CommonAssignData, Com_CommonApprovalWorkFlow = cw }).Join(
                        db.ApprovalTypes,
                        cw => cw.Com_CommonApprovalWorkFlow.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cw, at) => new { cw.Lev_LeaveHeaderHistory, cw.Lev_CancelLeaves, cw.Lev_LeaveType, cw.Com_CommonAssignData, cw.Com_CommonApprovalWorkFlow, ApprovalType = at }).Join(
                        db.Employees,
                        lh => lh.Lev_LeaveHeaderHistory.EmployeeId,
                        em => em.EmployeeId,
                        (lh, em) => new { lh.Lev_LeaveHeaderHistory, lh.Lev_CancelLeaves, lh.Lev_LeaveType, lh.Com_CommonAssignData, lh.Com_CommonApprovalWorkFlow, lh.ApprovalType, EmpName = em.FirstName + " " + em.LastName }).
                        Where(m => ((m.Com_CommonApprovalWorkFlow.ApprovePerson1 == EmployeeId && m.Lev_CancelLeaves.CancelApprovePerson1Status == "Rejected") ||
                        (m.Com_CommonApprovalWorkFlow.ApprovePerson2 == EmployeeId && m.Lev_CancelLeaves.CancelApprovePerson2Status == "Rejected") ||
                        (m.Com_CommonApprovalWorkFlow.ApprovePerson3 == EmployeeId && m.Lev_CancelLeaves.CancelApprovePerson3Status == "Rejected")) &&
                        m.ApprovalType.ApprovalTypeCode == "LA").ToList().Select(v => new
                        {
                            v.Lev_LeaveHeaderHistory.LeaveId,
                            v.Lev_CancelLeaves.LeaveDetailId,
                            EmployeeName = v.EmpName,
                            LeaveDate = v.Lev_CancelLeaves.LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                            v.Lev_CancelLeaves.LeaveQty,
                            v.Lev_LeaveHeaderHistory.EmployeeId,
                            Reason = v.Lev_CancelLeaves.CancelReason,
                            v.Lev_LeaveType.LeaveDescription,
                            v.Lev_CancelLeaves.CancelRejectReason,
                            v.Lev_CancelLeaves.LeaveCancelId,
                        }).ToList();

            return Json(new { data = leaveApproval }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult LeaveCancelApprove(int LeaveCancelId, string CreatedUser)
        {
            var result = false;
            try
            {
                long leaveEmployeeId = 0;
                string aprvPerson1Status = "";
                string aprvPerson2Status = "";
                string aprvPerson3Status = "";
                string approvePerson1 = "";
                string approvePerson2 = "";
                string approvePerson3 = "";
                DateTime leaveDate;
                int leaveTypeId;
                long leaveId;

                var lev = db.Lev_LeaveHeaderHistory.Join(
                        db.Lev_CancelLeaves.Where(x => x.LeaveCancelId == LeaveCancelId),
                        lh => lh.LeaveId,
                        ld => ld.LeaveId,
                        (lh, ld) => new { Lev_LeaveHeaderHistory = lh, Lev_CancelLeaves = ld }).
                        Select(v => new
                        {
                            v.Lev_LeaveHeaderHistory.EmployeeId,
                            v.Lev_CancelLeaves.CancelApprovePerson1Status,
                            v.Lev_CancelLeaves.CancelApprovePerson2Status,
                            v.Lev_CancelLeaves.CancelApprovePerson3Status,
                            v.Lev_CancelLeaves.LeaveTypeId,
                            v.Lev_CancelLeaves.LeaveDate,
                            v.Lev_CancelLeaves.LeaveId
                        }
                        ).ToList();

                leaveEmployeeId = lev.Select(x => x.EmployeeId).SingleOrDefault();

                var levWF = db.Com_CommonApprovalWorkFlow.Join(
                        db.Com_CommonAssignData,
                        cap => cap.ApprovalWorkFlowId,
                        cas => cas.ApprovalWorkFlowId,
                        (cap, cas) => new { Com_CommonApprovalWorkFlow = cap, Com_CommonAssignData = cas }).Join(
                        db.ApprovalTypes,
                        cas => cas.Com_CommonAssignData.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cas, at) => new { cas.Com_CommonApprovalWorkFlow, cas.Com_CommonAssignData, ApprovalType = at }).Where(
                        x => x.ApprovalType.ApprovalTypeCode == "LA" && x.Com_CommonAssignData.EmployeeId == leaveEmployeeId).Select(v => new
                        {
                            v.Com_CommonApprovalWorkFlow.ApprovePerson1,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson2,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson3
                        }).ToList();


                leaveTypeId = lev.Select(x => x.LeaveTypeId).SingleOrDefault();
                aprvPerson1Status = lev.Select(x => x.CancelApprovePerson1Status).SingleOrDefault();
                aprvPerson2Status = lev.Select(x => x.CancelApprovePerson2Status).SingleOrDefault();
                aprvPerson3Status = lev.Select(x => x.CancelApprovePerson3Status).SingleOrDefault();
                approvePerson1 = levWF.Select(x => x.ApprovePerson1).FirstOrDefault().ToString();
                approvePerson2 = levWF.Select(x => x.ApprovePerson2).FirstOrDefault().ToString();
                approvePerson3 = levWF.Select(x => x.ApprovePerson3).FirstOrDefault().ToString();
                leaveDate = lev.Select(x => x.LeaveDate).SingleOrDefault();
                leaveId = lev.Select(x => x.LeaveId).SingleOrDefault();

                if (lev != null && levWF != null)
                {
                    var leaveDetail = db.Lev_CancelLeaves.Where(x => x.LeaveCancelId == LeaveCancelId).FirstOrDefault();

                    var objTimeTxn = db.Time_EmployeeTxn.Where(x => x.EmployeeId == leaveEmployeeId && x.Date == leaveDate).FirstOrDefault();

                    if (!approvePerson1.Equals("") && !approvePerson2.Equals("") && !approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            leaveDetail.CancelApprovePerson1Status = "Approved";
                            leaveDetail.CancelApprovePerson2Status = "Pending";
                            leaveDetail.CancelApprovePerson1 = Convert.ToInt64(approvePerson1);
                            leaveDetail.CancelApprovePerson2 = Convert.ToInt64(approvePerson2);
                            db.SaveChanges();

                            InsertLeaveNotificationWhenLeaveCancelApprove(Convert.ToInt64(approvePerson2), leaveEmployeeId);
                            SendEmailToLeaveCancelApprovePerson(LeaveCancelId, Convert.ToInt64(approvePerson2));
                        }
                        else if (aprvPerson2Status == "Pending")
                        {
                            leaveDetail.CancelApprovePerson2Status = "Approved";
                            leaveDetail.CancelApprovePerson3Status = "Pending";
                            leaveDetail.CancelApprovePerson2 = Convert.ToInt64(approvePerson2);
                            leaveDetail.CancelApprovePerson3 = Convert.ToInt64(approvePerson3);
                            db.SaveChanges();

                            InsertLeaveNotificationWhenLeaveCancelApprove(Convert.ToInt64(approvePerson3), leaveEmployeeId);
                            SendEmailToLeaveCancelApprovePerson(LeaveCancelId, Convert.ToInt64(approvePerson3));
                        }
                        else if (aprvPerson3Status == "Pending")
                        {
                            leaveDetail.CancelStatus = "Approved";
                            leaveDetail.CancelApprovePerson3Status = "Approved";
                            leaveDetail.CancelApprovePerson3 = Convert.ToInt64(approvePerson3);
                            db.SaveChanges();

                            LeaveCancelCalculation(leaveDetail.LeaveDetailId, leaveId, leaveEmployeeId, leaveDate, CreatedUser);
                        }
                    }
                    else if (!approvePerson1.Equals("") && !approvePerson2.Equals("") && approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            leaveDetail.CancelApprovePerson1Status = "Approved";
                            leaveDetail.CancelApprovePerson2Status = "Pending";
                            leaveDetail.CancelApprovePerson1 = Convert.ToInt64(approvePerson1);
                            leaveDetail.CancelApprovePerson2 = Convert.ToInt64(approvePerson2);
                            db.SaveChanges();

                            InsertLeaveNotificationWhenLeaveCancelApprove(Convert.ToInt64(approvePerson2), leaveEmployeeId);
                            SendEmailToLeaveCancelApprovePerson(LeaveCancelId, Convert.ToInt64(approvePerson2));
                        }
                        else if (aprvPerson2Status == "Pending")
                        {
                            leaveDetail.CancelStatus = "Approved";
                            leaveDetail.CancelApprovePerson2Status = "Approved";
                            leaveDetail.CancelApprovePerson2 = Convert.ToInt64(approvePerson2);
                            db.SaveChanges();

                            LeaveCancelCalculation(leaveDetail.LeaveDetailId, leaveId, leaveEmployeeId, leaveDate, CreatedUser);
                        }
                    }
                    else if (!approvePerson1.Equals("") && approvePerson2.Equals("") && approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            leaveDetail.CancelStatus = "Approved";
                            leaveDetail.CancelApprovePerson1Status = "Approved";
                            leaveDetail.CancelApprovePerson1 = Convert.ToInt64(approvePerson1);
                            db.SaveChanges();

                            LeaveCancelCalculation(leaveDetail.LeaveDetailId, leaveId, leaveEmployeeId, leaveDate, CreatedUser);
                        }
                    }
                }

                result = true;

                return Json(new { success = result, message = "Submitted Successfully" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = result, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult LeaveCancelReject(int LeaveCancelId, string RejectedReason, string CreatedUser)
        {
            var result = false;
            try
            {
                long leaveEmployeeId = 0;
                string aprvPerson1Status = "";
                string aprvPerson2Status = "";
                string aprvPerson3Status = "";
                string approvePerson1 = "";
                string approvePerson2 = "";
                string approvePerson3 = "";
                DateTime leaveDate;
                int leaveTypeId;
                decimal leaveQty;

                var lev = db.Lev_LeaveHeaderHistory.Join(
                        db.Lev_CancelLeaves.Where(x => x.LeaveCancelId == LeaveCancelId),
                        lh => lh.LeaveId,
                        ld => ld.LeaveId,
                        (lh, ld) => new { Lev_LeaveHeaderHistory = lh, Lev_CancelLeaves = ld }).
                        Select(v => new
                        {
                            v.Lev_LeaveHeaderHistory.EmployeeId,
                            v.Lev_CancelLeaves.CancelApprovePerson1Status,
                            v.Lev_CancelLeaves.CancelApprovePerson2Status,
                            v.Lev_CancelLeaves.CancelApprovePerson3Status,
                            v.Lev_CancelLeaves.LeaveTypeId,
                            v.Lev_CancelLeaves.LeaveDate
                        }
                        ).ToList();

                leaveEmployeeId = lev.Select(x => x.EmployeeId).SingleOrDefault();

                var levWF = db.Com_CommonApprovalWorkFlow.Join(
                        db.Com_CommonAssignData,
                        cap => cap.ApprovalWorkFlowId,
                        cas => cas.ApprovalWorkFlowId,
                        (cap, cas) => new { Com_CommonApprovalWorkFlow = cap, Com_CommonAssignData = cas }).Join(
                        db.ApprovalTypes,
                        cas => cas.Com_CommonAssignData.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cas, at) => new { cas.Com_CommonApprovalWorkFlow, cas.Com_CommonAssignData, ApprovalType = at }).Where(
                        x => x.ApprovalType.ApprovalTypeCode == "LA" && x.Com_CommonAssignData.EmployeeId == leaveEmployeeId).Select(v => new
                        {
                            v.Com_CommonApprovalWorkFlow.ApprovePerson1,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson2,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson3
                        }).ToList();


                leaveTypeId = lev.Select(x => x.LeaveTypeId).SingleOrDefault();
                aprvPerson1Status = lev.Select(x => x.CancelApprovePerson1Status).SingleOrDefault();
                aprvPerson2Status = lev.Select(x => x.CancelApprovePerson2Status).SingleOrDefault();
                aprvPerson3Status = lev.Select(x => x.CancelApprovePerson3Status).SingleOrDefault();
                approvePerson1 = levWF.Select(x => x.ApprovePerson1).FirstOrDefault().ToString();
                approvePerson2 = levWF.Select(x => x.ApprovePerson2).FirstOrDefault().ToString();
                approvePerson3 = levWF.Select(x => x.ApprovePerson3).FirstOrDefault().ToString();
                leaveDate = lev.Select(x => x.LeaveDate).SingleOrDefault();

                if (lev != null && levWF != null)
                {
                    var leaveDetail = db.Lev_CancelLeaves.Where(x => x.LeaveCancelId == LeaveCancelId).FirstOrDefault();

                    var objTimeTxn = db.Time_EmployeeTxn.Where(x => x.EmployeeId == leaveEmployeeId && x.Date == leaveDate).FirstOrDefault();

                    if (!approvePerson1.Equals("") && !approvePerson2.Equals("") && !approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            leaveDetail.CancelApprovePerson1Status = "Rejected";
                            leaveDetail.CancelStatus = "Rejected";
                            leaveDetail.CancelApprovePerson1 = Convert.ToInt64(approvePerson1);
                            leaveDetail.CancelRejectPerson = Convert.ToInt64(approvePerson1);
                            leaveDetail.CancelRejectReason = RejectedReason;
                            db.SaveChanges();

                            InsertNotificationWhenRejectLeaveCancel(Convert.ToInt64(approvePerson1), leaveEmployeeId, leaveDate);
                            SendEmailWhenLeaveCancelationRejected(LeaveCancelId, Convert.ToInt64(approvePerson1));
                        }
                        else if (aprvPerson2Status == "Pending")
                        {
                            leaveDetail.CancelApprovePerson2Status = "Rejected";
                            leaveDetail.CancelStatus = "Rejected";
                            leaveDetail.CancelApprovePerson2 = Convert.ToInt64(approvePerson2);
                            leaveDetail.CancelRejectPerson = Convert.ToInt64(approvePerson2);
                            leaveDetail.CancelRejectReason = RejectedReason;
                            db.SaveChanges();

                            InsertNotificationWhenRejectLeaveCancel(Convert.ToInt64(approvePerson2), leaveEmployeeId, leaveDate);
                            SendEmailWhenLeaveCancelationRejected(LeaveCancelId, Convert.ToInt64(approvePerson2));
                        }
                        else if (aprvPerson3Status == "Pending")
                        {
                            leaveDetail.CancelApprovePerson3Status = "Rejected";
                            leaveDetail.CancelStatus = "Rejected";
                            leaveDetail.CancelApprovePerson3 = Convert.ToInt64(approvePerson3);
                            leaveDetail.CancelRejectPerson = Convert.ToInt64(approvePerson3);
                            leaveDetail.CancelRejectReason = RejectedReason;
                            db.SaveChanges();

                            InsertNotificationWhenRejectLeaveCancel(Convert.ToInt64(approvePerson3), leaveEmployeeId, leaveDate);
                            SendEmailWhenLeaveCancelationRejected(LeaveCancelId, Convert.ToInt64(approvePerson3));
                        }
                    }
                    else if (!approvePerson1.Equals("") && !approvePerson2.Equals("") && approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            leaveDetail.CancelApprovePerson1Status = "Rejected";
                            leaveDetail.CancelStatus = "Rejected";
                            leaveDetail.CancelApprovePerson1 = Convert.ToInt64(approvePerson1);
                            leaveDetail.CancelRejectPerson = Convert.ToInt64(approvePerson1);
                            leaveDetail.CancelRejectReason = RejectedReason;
                            db.SaveChanges();

                            InsertNotificationWhenRejectLeaveCancel(Convert.ToInt64(approvePerson1), leaveEmployeeId, leaveDate);
                            SendEmailWhenLeaveCancelationRejected(LeaveCancelId, Convert.ToInt64(approvePerson1));
                        }
                        else if (aprvPerson2Status == "Pending")
                        {
                            leaveDetail.CancelApprovePerson2Status = "Rejected";
                            leaveDetail.CancelStatus = "Rejected";
                            leaveDetail.CancelApprovePerson2 = Convert.ToInt64(approvePerson2);
                            leaveDetail.CancelRejectPerson = Convert.ToInt64(approvePerson2);
                            leaveDetail.CancelRejectReason = RejectedReason;
                            db.SaveChanges();

                            InsertNotificationWhenRejectLeaveCancel(Convert.ToInt64(approvePerson2), leaveEmployeeId, leaveDate);
                            SendEmailWhenLeaveCancelationRejected(LeaveCancelId, Convert.ToInt64(approvePerson2));
                        }
                    }
                    else if (!approvePerson1.Equals("") && approvePerson2.Equals("") && approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            leaveDetail.CancelApprovePerson1Status = "Rejected";
                            leaveDetail.CancelStatus = "Rejected";
                            leaveDetail.CancelApprovePerson1 = Convert.ToInt64(approvePerson1);
                            leaveDetail.CancelRejectPerson = Convert.ToInt64(approvePerson1);
                            leaveDetail.CancelRejectReason = RejectedReason;
                            db.SaveChanges();

                            InsertNotificationWhenRejectLeaveCancel(Convert.ToInt64(approvePerson1), leaveEmployeeId, leaveDate);
                            SendEmailWhenLeaveCancelationRejected(LeaveCancelId, Convert.ToInt64(approvePerson1));
                        }
                    }
                }

                result = true;

                return Json(new { success = result, message = "Submitted Successfully" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = result, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public void InsertNotificationWhenRejectLeaveCancel(long RejectEmployeeId, long LeaveEmployeeId, DateTime LeaveDate)
        {
            string content = "";
            string functionName = "";
            var rejectedEmployeeName = db.Employees.Where(x => x.EmployeeId == RejectEmployeeId).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault();
            content = LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + " leave cancellation request have rejected by " + rejectedEmployeeName + ".";
            functionName = "LeaveHistory";

            Por_Notification noti = new Por_Notification();
            noti.EmployeeId = LeaveEmployeeId;
            noti.NotificationTypeId = 2;
            noti.Content = content;
            noti.ControllerName = "Leave";
            noti.FunctionName = functionName;
            noti.IsExpired = false;
            noti.CreatedDate = DateTime.Now;
            db.Por_Notification.Add(noti);
            db.SaveChanges();
        }
        public void InsertLeaveNotificationToAppliedEmp(long EmployeeId, long ApprovePerson1, DateTime leaveDate)
        {
            string content = "";
            string functionName = "";
            var leaveApprovedName = db.Employees.Where(x => x.EmployeeId == ApprovePerson1).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault();


            content = leaveApprovedName + " has approved leave request on " + leaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            functionName = "LeaveApprove";


            Por_Notification noti = new Por_Notification();
            noti.EmployeeId = EmployeeId;
            noti.NotificationTypeId = 1;
            noti.Content = content;
            noti.ControllerName = "Leave";
            noti.FunctionName = functionName;
            noti.IsExpired = false;
            noti.CreatedDate = DateTime.Now;
            db.Por_Notification.Add(noti);
            db.SaveChanges();
        }


        public ActionResult LeaveDetailsReport(int? LeaveId)
        {
            Int32 empId = Convert.ToInt32(Session["EmployeeId"].ToString());
            var stream = new MemoryStream();

            CounsilerAnnualReport CounsilerReport = new CounsilerAnnualReport();

            CounsilerReport.Parameters["OrgStructureID"].Value = 0;
            CounsilerReport.Parameters["Company"].Value = 0;
            CounsilerReport.Parameters["EmpID"].Value =0;
            CounsilerReport.Parameters["CatID"].Value = 0;
            CounsilerReport.Parameters["ReportValue"].Value = 0;
            CounsilerReport.Parameters["UserName"].Value = "infox";
            CounsilerReport.Parameters["Sortby"].Value = 0;
            CounsilerReport.Parameters["FromDate"].Value = new DateTime(2024, 9, 1);
            CounsilerReport.Parameters["ToDate"].Value = new DateTime(2024, 9, 1); 
            CounsilerReport.Parameters["OrgLevel"].Value = 0;
            CounsilerReport.Parameters["LeaveId"].Value = LeaveId;
           




            CounsilerReport.ExportToPdf(stream);
            var cd = new System.Net.Mime.ContentDisposition
            {
                FileName = "LeaveDeatils.pdf",
                Inline = false,
            };
            Response.AppendHeader("Content-Disposition", cd.ToString());
            return File(stream.ToArray(), "application/pdf");
        }

        public ActionResult LeavestaffDetailsReport(int? LeaveId)
        {
            Int32 empId = Convert.ToInt32(Session["EmployeeId"].ToString());
            var stream = new MemoryStream();

            StaffAnnualReport staffReport = new StaffAnnualReport();

            staffReport.Parameters["OrgStructureID"].Value = 0;
            staffReport.Parameters["Company"].Value = 0;
            staffReport.Parameters["EmpID"].Value = 0;
            staffReport.Parameters["CatID"].Value = 0;
            staffReport.Parameters["ReportValue"].Value = 0;
            staffReport.Parameters["UserName"].Value = "infox";
            staffReport.Parameters["Sortby"].Value = 0;
            staffReport.Parameters["FromDate"].Value = new DateTime(2024, 9, 1);
            staffReport.Parameters["ToDate"].Value = new DateTime(2024, 9, 1);
            staffReport.Parameters["OrgLevel"].Value = 0;
            staffReport.Parameters["LeaveId"].Value = LeaveId;





            staffReport.ExportToPdf(stream);
            var cd = new System.Net.Mime.ContentDisposition
            {
                FileName = "LeaveDeatils.pdf",
                Inline = false,
            };
            Response.AppendHeader("Content-Disposition", cd.ToString());
            return File(stream.ToArray(), "application/pdf");
        }
    }
}