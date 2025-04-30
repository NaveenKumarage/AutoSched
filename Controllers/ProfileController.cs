using CRUD_OperationByMeUsingJqueryAjaxMvc.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace CRUD_OperationByMeUsingJqueryAjaxMvc.Controllers
{
    public class ProfileController : Controller
    {
        Dhigurah_DBEntities db = new Dhigurah_DBEntities();

        public ActionResult Profile()
        {
            return View();
        }

        public JsonResult GetEmployeeDetails(long EmployeeId)
        {
            var empDetails = db.Employees.Where(x => x.EmployeeId == EmployeeId).Join(
                    db.DesignationStuctures,
                    em => em.DesignationID,
                    ds => ds.DesignationStuctureID,
                    (em, ds) => new { Employee = em, DesignationStucture = ds }).Join(
                    db.Designations,
                    ds => ds.DesignationStucture.DesignationID,
                    de => de.DesignationID,
                    (ds, de) => new { ds.Employee, ds.DesignationStucture, Designation = de }).ToList().Select(x => new
                    {
                        x.Employee.EmployeeCode,
                        x.Employee.EPFNo,
                        x.Employee.FirstName,
                        x.Employee.LastName,
                        x.Employee.NameWithInitial,
                        x.Employee.FullName,
                        x.Employee.CallName,
                        x.Employee.NIC,
                        x.Employee.Gender,
                        x.Employee.Status,
                        DateOfBirth = x.Employee.DateOfBirth.ToString("MM/dd/yyyy"),
                        x.Employee.Address,
                        x.Employee.PostalCode,
                        x.Employee.MobileNo,
                        x.Employee.HomeContactNo,
                        x.Employee.OfficeContactNo,
                        x.Employee.Email,
                        x.Employee.PassportNo,
                        PassPortExpiryDate = x.Employee.PassPortExpiryDate?.ToString("MM/dd/yyyy"),
                        x.Employee.EmergencyContactPerson,
                        x.Employee.EmergencyContactNo,
                        x.Employee.RelationshipOfContactPerson,
                        x.Designation.DesignationName
                    }).ToList();

            return Json(new { dataList = empDetails }, JsonRequestBehavior.AllowGet);
        }

        public JsonResult UpdateProfileDetails()
        {
            bool result = false;
            string notification = "";
            string notificationFull = "";
            bool isCheckNext = true;
            DateTime? passportExpiaryDateDateTime = null;
            try
            {
                long employeeId = Convert.ToInt64(Request.Form["EmployeeId"]);
                string firstName = Request.Form["FirstName"];
                string lastName = Request.Form["LastName"];
                string initials = Request.Form["Initials"];
                string fullName = Request.Form["FullName"];
                string callName = Request.Form["CallName"];
                string gender = Request.Form["Gender"];
                string status = Request.Form["Status"];
                string dateOfBirth = Request.Form["DateOfBirth"];
                string address = Request.Form["Address"];
                string postalCode = Request.Form["PostalCode"];
                string contactNoMobile = Request.Form["ContactNoMobile"];
                string contactNoHome = Request.Form["ContactNoHome"];
                string officeNo = Request.Form["OfficeNo"];
                string email = Request.Form["Email"];
                string nicNo = Request.Form["NicNo"];
                string passportNo = Request.Form["PassportNo"];
                string passportExpiaryDate = Request.Form["PassportExpiaryDate"];
                string emergencyContactPerson = Request.Form["EmergencyContactPerson"];
                string emergencyContactNo = Request.Form["EmergencyContactNo"];
                string relationship = Request.Form["Relationship"];
                string userName = Request.Form["UserName"];

                if (passportExpiaryDate != "")
                {
                    passportExpiaryDateDateTime = Convert.ToDateTime(passportExpiaryDate);
                }

                var employee = db.Employees.Where(x => x.EmployeeId == employeeId).FirstOrDefault();
                if(employee != null)
                {
                    var previousChanges = db.POR_ChangeProfileDetails.Where(x => x.EmployeeId == employeeId && x.Status == "Pending").ToList();
                    if(previousChanges.Count > 0)
                    {
                        if (firstName != (employee.FirstName ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "FirstName").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if(notification.Length > 0)
                                {
                                    notification = notification + ", 'First Name'";
                                }
                                else
                                {
                                    notification = "'First Name'";
                                }
                                
                                isCheckNext = false;
                            }
                        }

                        if (lastName != (employee.LastName ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "LastName").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Last Name'";
                                }
                                else
                                {
                                    notification = "'Last Name'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (initials != (employee.NameWithInitial ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "NameWithInitial").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Initials'";
                                }
                                else
                                {
                                    notification = "'Initials'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (fullName != (employee.FullName ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "FullName").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Full Name'";
                                }
                                else
                                {
                                    notification = "'Full Name'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (callName != (employee.CallName ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "CallName").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Call Name'";
                                }
                                else
                                {
                                    notification = "'Call Name'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (gender != (employee.Gender ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "Gender").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Gender'";
                                }
                                else
                                {
                                    notification = "'Gender'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (Convert.ToInt32(status) != employee.Status)
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "Status").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Status'";
                                }
                                else
                                {
                                    notification = "'Status'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (Convert.ToDateTime(dateOfBirth) != employee.DateOfBirth)
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "DateOfBirth").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Date Of Birth'";
                                }
                                else
                                {
                                    notification = "'Date Of Birth'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (address != (employee.Address ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "Address").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Address'";
                                }
                                else
                                {
                                    notification = "'Address'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (postalCode != (employee.PostalCode ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "PostalCode").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Postal Code'";
                                }
                                else
                                {
                                    notification = "'Postal Code'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (contactNoMobile != (employee.MobileNo ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "MobileNo").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Contact No (Mobile)'";
                                }
                                else
                                {
                                    notification = "'Contact No (Mobile)'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (contactNoHome != (employee.HomeContactNo ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "HomeContactNo").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Contact No (Home)'";
                                }
                                else
                                {
                                    notification = "'Contact No (Home)'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (officeNo != (employee.OfficeContactNo ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "OfficeContactNo").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Office No'";
                                }
                                else
                                {
                                    notification = "'Office No'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (email != (employee.Email ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "Email").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Email'";
                                }
                                else
                                {
                                    notification = "'Email'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (nicNo != (employee.NIC ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "NIC").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'NIC No'";
                                }
                                else
                                {
                                    notification = "'NIC No'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (passportNo != (employee.PassportNo ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "PassportNo").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Passport No'";
                                }
                                else
                                {
                                    notification = "'Passport No'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (passportExpiaryDateDateTime != employee.PassPortExpiryDate && isCheckNext)
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "PassPortExpiryDate").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Passport Expiary Date'";
                                }
                                else
                                {
                                    notification = "'Passport Expiary Date'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (emergencyContactPerson != (employee.EmergencyContactPerson ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "EmergencyContactPerson").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Emergency Contact Person'";
                                }
                                else
                                {
                                    notification = "'Emergency Contact Person'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (emergencyContactNo != (employee.EmergencyContactNo ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "EmergencyContactNo").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Emergency Contact No'";
                                }
                                else
                                {
                                    notification = "'Emergency Contact No'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (relationship != (employee.RelationshipOfContactPerson ?? ""))
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "RelationshipOfContactPerson").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Relationship'";
                                }
                                else
                                {
                                    notification = "'Relationship'";
                                }

                                isCheckNext = false;
                            }
                        }

                        if (Request.Files.Count > 0)
                        {
                            var alreadyPending = previousChanges.Where(x => x.RelatedTableName == "Employee" && x.RelatedColumnName == "Image").FirstOrDefault();
                            if (alreadyPending != null)
                            {
                                if (notification.Length > 0)
                                {
                                    notification = notification + ", 'Change Profile Image'";
                                }
                                else
                                {
                                    notification = "'Change Profile Image'";
                                }

                                isCheckNext = false;
                            }
                        }
                    }

                    if (isCheckNext == false)
                    {
                        notificationFull = "Can't request. Already have a pending record for " + notification + "";
                    }
                    else
                    {
                        if (firstName != (employee.FirstName ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "FirstName", "First Name", firstName, firstName, userName);
                        }

                        if (lastName != (employee.LastName ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "LastName", "Last Name", lastName, lastName, userName);
                        }

                        if (initials != (employee.NameWithInitial ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "NameWithInitial", "Initials", initials, initials, userName);
                        }

                        if (fullName != (employee.FullName ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "FullName", "Full Name", fullName, fullName, userName);
                        }

                        if (callName != (employee.CallName ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "CallName", "Call Name", callName, callName, userName);
                        }

                        if (gender != (employee.Gender ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "Gender", "Gender", gender, gender, userName);
                        }

                        if (Convert.ToInt32(status) != employee.Status && isCheckNext)
                        {
                            SaveToChangeProfile(employeeId, "Employee", "Status", "Status", status, status, userName);
                        }

                        if (Convert.ToDateTime(dateOfBirth) != employee.DateOfBirth && isCheckNext)
                        {
                            SaveToChangeProfile(employeeId, "Employee", "DateOfBirth", "Date Of Birth", dateOfBirth, dateOfBirth, userName);
                        }

                        if (address != (employee.Address ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "Address", "Address", address, address, userName);
                        }

                        if (postalCode != (employee.PostalCode ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "PostalCode", "Postal Code", postalCode, postalCode, userName);
                        }

                        if (contactNoMobile != (employee.MobileNo ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "MobileNo", "Contact No (Mobile)", contactNoMobile, contactNoMobile, userName);
                        }

                        if (contactNoHome != (employee.HomeContactNo ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "HomeContactNo", "Contact No (Home)", contactNoHome, contactNoHome, userName);
                        }

                        if (officeNo != (employee.OfficeContactNo ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "OfficeContactNo", "Office No", officeNo, officeNo, userName);
                        }

                        if (email != (employee.Email ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "Email", "Email", email, email, userName);
                        }

                        if (nicNo != (employee.NIC ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "NIC", "NIC No", nicNo, nicNo, userName);
                        }

                        if (passportNo != (employee.PassportNo ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "PassportNo", "Passport No", passportNo, passportNo, userName);
                        }

                        if (passportExpiaryDateDateTime != employee.PassPortExpiryDate && isCheckNext)
                        {
                            SaveToChangeProfile(employeeId, "Employee", "PassPortExpiryDate", "Passport Expiary Date", passportExpiaryDate, passportExpiaryDate, userName);
                        }

                        if (emergencyContactPerson != (employee.EmergencyContactPerson ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "EmergencyContactPerson", "Emergency Contact Person", emergencyContactPerson, emergencyContactPerson, userName);
                        }

                        if (emergencyContactNo != (employee.EmergencyContactNo ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "EmergencyContactNo", "Emergency Contact No", emergencyContactNo, emergencyContactNo, userName);
                        }

                        if (relationship != (employee.RelationshipOfContactPerson ?? ""))
                        {
                            SaveToChangeProfile(employeeId, "Employee", "RelationshipOfContactPerson", "Relationship Of Contact Person", relationship, relationship, userName);
                        }

                        if (Request.Files.Count > 0)
                        {
                            var file = Request.Files[0];
                            string fileName = Path.GetFileNameWithoutExtension(file.FileName);
                            string extension = Path.GetExtension(file.FileName);
                            fileName = fileName + DateTime.Now.ToString("yymmssfff") + extension;
                            string trueValue = fileName;
                            string webConfigPath = ConfigurationManager.AppSettings["ProfileImageUploadPath"];
                            string filePath = webConfigPath + fileName;
                            file.SaveAs(filePath);

                            SaveToChangeProfile(employeeId, "Employee", "Image", "Profile Image", filePath, trueValue, userName);
                        }

                        if (isCheckNext)
                        {
                            result = true;
                            notificationFull = "Changes send to approve person";
                        }
                    }
                }
                
            }
            catch (Exception ex)
            {
                notificationFull = ex.Message;
            }


            return Json(new { success = result, message = notificationFull }, JsonRequestBehavior.AllowGet);
        }

        public void SaveToChangeProfile(long EmployeeId, string RelatedTableName, string RelatedColumnName, string RelatedFieldName, string Value, string TrueValue, string CreatedUser)
        {
            var profileChange = new POR_ChangeProfileDetails();
            profileChange.EmployeeId = EmployeeId;
            profileChange.RelatedTableName = RelatedTableName;
            profileChange.RelatedColumnName = RelatedColumnName;
            profileChange.RelatedFieldName = RelatedFieldName;
            profileChange.Value = Value;
            profileChange.TrueValue = TrueValue;
            profileChange.ApprovePerson1Status = "Pending";
            profileChange.Status = "Pending";
            profileChange.IsWorkFlow = true;
            if (RelatedColumnName == "Image")
            {
                profileChange.IsImage = true;
            }
            profileChange.CreatedUser = CreatedUser;
            profileChange.CreatedDate = DateTime.Now;
            db.POR_ChangeProfileDetails.Add(profileChange);
            db.SaveChanges();
        }

        //-----------------------------------------------------------------------------------Profile Details Approval----------------------------------------------------------------------------------------------------------------------------------------
        public ActionResult ProfileDetailsApproval()
        {
            return View();
        }

        public ActionResult GetPendingProfileDetailsApprovals(int EmployeeId)
        {
            db.Configuration.ProxyCreationEnabled = false;
            var proApproval = db.POR_ChangeProfileDetails.Join(
                    db.Employees,
                    aa => aa.EmployeeId,
                    em => em.EmployeeId,
                    (aa, em) => new { POR_ChangeProfileDetails = aa, Employee = em }).Join(
                    db.Com_CommonAssignData,
                    aa => aa.POR_ChangeProfileDetails.EmployeeId,
                    ca => ca.EmployeeId,
                    (aa, ca) => new { aa.POR_ChangeProfileDetails, aa.Employee, Com_CommonAssignData = ca }).Join(
                    db.Com_CommonApprovalWorkFlow,
                    ca => ca.Com_CommonAssignData.ApprovalWorkFlowId,
                    aw => aw.ApprovalWorkFlowId,
                    (ca, aw) => new { ca.POR_ChangeProfileDetails, ca.Employee, ca.Com_CommonAssignData, Com_CommonApprovalWorkFlow = aw }).Join(
                    db.ApprovalTypes,
                    aw => aw.Com_CommonApprovalWorkFlow.ApprovalTypeID,
                    at => at.ApprovalTypeID,
                    (aw, at) => new { aw.POR_ChangeProfileDetails, aw.Employee, aw.Com_CommonAssignData, aw.Com_CommonApprovalWorkFlow, ApprovalType = at }).
                    Where(x => ((x.Com_CommonApprovalWorkFlow.ApprovePerson1 == EmployeeId && x.POR_ChangeProfileDetails.ApprovePerson1Status == "Pending") ||
                    (x.Com_CommonApprovalWorkFlow.ApprovePerson2 == EmployeeId && x.POR_ChangeProfileDetails.ApprovePerson2Status == "Pending") ||
                    (x.Com_CommonApprovalWorkFlow.ApprovePerson3 == EmployeeId && x.POR_ChangeProfileDetails.ApprovePerson3Status == "Pending")) &&
                    x.POR_ChangeProfileDetails.IsWorkFlow == true && x.ApprovalType.ApprovalTypeCode == "PCA").ToList().Select(x => new
                    {
                        EmployeeName = x.Employee.EmployeeCode + "/" + x.Employee.NameWithInitial + " " + x.Employee.LastName,
                        Date = x.POR_ChangeProfileDetails.CreatedDate?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture),
                        x.POR_ChangeProfileDetails.ID,
                        x.POR_ChangeProfileDetails.RelatedFieldName,
                        x.POR_ChangeProfileDetails.Value,
                        x.POR_ChangeProfileDetails.IsImage
                    }).ToList();

            return Json(new { data = proApproval }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetApprovedProfileDetailsApprovals(int EmployeeId)
        {
            db.Configuration.ProxyCreationEnabled = false;
            var proApproval = db.POR_ChangeProfileDetails.Join(
                    db.Employees,
                    aa => aa.EmployeeId,
                    em => em.EmployeeId,
                    (aa, em) => new { POR_ChangeProfileDetails = aa, Employee = em }).Join(
                    db.Com_CommonAssignData,
                    aa => aa.POR_ChangeProfileDetails.EmployeeId,
                    ca => ca.EmployeeId,
                    (aa, ca) => new { aa.POR_ChangeProfileDetails, aa.Employee, Com_CommonAssignData = ca }).Join(
                    db.Com_CommonApprovalWorkFlow,
                    ca => ca.Com_CommonAssignData.ApprovalWorkFlowId,
                    aw => aw.ApprovalWorkFlowId,
                    (ca, aw) => new { ca.POR_ChangeProfileDetails, ca.Employee, ca.Com_CommonAssignData, Com_CommonApprovalWorkFlow = aw }).Join(
                    db.ApprovalTypes,
                    aw => aw.Com_CommonApprovalWorkFlow.ApprovalTypeID,
                    at => at.ApprovalTypeID,
                    (aw, at) => new { aw.POR_ChangeProfileDetails, aw.Employee, aw.Com_CommonAssignData, aw.Com_CommonApprovalWorkFlow, ApprovalType = at }).
                    Where(x => ((x.Com_CommonApprovalWorkFlow.ApprovePerson1 == EmployeeId && x.POR_ChangeProfileDetails.ApprovePerson1Status == "Approved") ||
                    (x.Com_CommonApprovalWorkFlow.ApprovePerson2 == EmployeeId && x.POR_ChangeProfileDetails.ApprovePerson2Status == "Approved") ||
                    (x.Com_CommonApprovalWorkFlow.ApprovePerson3 == EmployeeId && x.POR_ChangeProfileDetails.ApprovePerson3Status == "Approved")) &&
                    x.POR_ChangeProfileDetails.IsWorkFlow == true && x.ApprovalType.ApprovalTypeCode == "PCA").ToList().Select(x => new
                    {
                        EmployeeName = x.Employee.EmployeeCode + "/" + x.Employee.NameWithInitial + " " + x.Employee.LastName,
                        Date = x.POR_ChangeProfileDetails.CreatedDate?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture),
                        x.POR_ChangeProfileDetails.ID,
                        x.POR_ChangeProfileDetails.RelatedFieldName,
                        x.POR_ChangeProfileDetails.Value,
                        x.POR_ChangeProfileDetails.IsImage
                    }).ToList();

            return Json(new { data = proApproval }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetRejectedProfileDetailsApprovals(int EmployeeId)
        {
            db.Configuration.ProxyCreationEnabled = false;
            var proApproval = db.POR_ChangeProfileDetails.Join(
                    db.Employees,
                    aa => aa.EmployeeId,
                    em => em.EmployeeId,
                    (aa, em) => new { POR_ChangeProfileDetails = aa, Employee = em }).Join(
                    db.Com_CommonAssignData,
                    aa => aa.POR_ChangeProfileDetails.EmployeeId,
                    ca => ca.EmployeeId,
                    (aa, ca) => new { aa.POR_ChangeProfileDetails, aa.Employee, Com_CommonAssignData = ca }).Join(
                    db.Com_CommonApprovalWorkFlow,
                    ca => ca.Com_CommonAssignData.ApprovalWorkFlowId,
                    aw => aw.ApprovalWorkFlowId,
                    (ca, aw) => new { ca.POR_ChangeProfileDetails, ca.Employee, ca.Com_CommonAssignData, Com_CommonApprovalWorkFlow = aw }).Join(
                    db.ApprovalTypes,
                    aw => aw.Com_CommonApprovalWorkFlow.ApprovalTypeID,
                    at => at.ApprovalTypeID,
                    (aw, at) => new { aw.POR_ChangeProfileDetails, aw.Employee, aw.Com_CommonAssignData, aw.Com_CommonApprovalWorkFlow, ApprovalType = at }).
                    Where(x => ((x.Com_CommonApprovalWorkFlow.ApprovePerson1 == EmployeeId && x.POR_ChangeProfileDetails.ApprovePerson1Status == "Rejected") ||
                    (x.Com_CommonApprovalWorkFlow.ApprovePerson2 == EmployeeId && x.POR_ChangeProfileDetails.ApprovePerson2Status == "Rejected") ||
                    (x.Com_CommonApprovalWorkFlow.ApprovePerson3 == EmployeeId && x.POR_ChangeProfileDetails.ApprovePerson3Status == "Rejected")) &&
                    x.POR_ChangeProfileDetails.IsWorkFlow == true && x.ApprovalType.ApprovalTypeCode == "PCA").ToList().Select(x => new
                    {
                        EmployeeName = x.Employee.EmployeeCode + "/" + x.Employee.NameWithInitial + " " + x.Employee.LastName,
                        Date = x.POR_ChangeProfileDetails.CreatedDate?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture),
                        x.POR_ChangeProfileDetails.ID,
                        x.POR_ChangeProfileDetails.RelatedFieldName,
                        x.POR_ChangeProfileDetails.Value,
                        x.POR_ChangeProfileDetails.IsImage
                    }).ToList();

            return Json(new { data = proApproval }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult ApprovePersonApprove(int ApprovalId, string ApprovedReason, string ModifiedUser)
        {
            var result = false;
            try
            {
                long attEmployeeId = 0;
                string aprvPerson1Status = "";
                string aprvPerson2Status = "";
                string aprvPerson3Status = "";
                string approvePerson1 = "";
                string approvePerson2 = "";
                string approvePerson3 = "";
                string relatedTableName = "";
                string relatedColumnName = "";
                string ChangedValue = "";
                

                var attAppRec = db.POR_ChangeProfileDetails.Where(x => x.ID == ApprovalId).Select(x => new
                {
                    x.EmployeeId,
                    x.RelatedTableName,
                    x.RelatedColumnName,
                    x.TrueValue,
                    x.ApprovePerson1Status,
                    x.ApprovePerson2Status,
                    x.ApprovePerson3Status
                }).ToList();

                attEmployeeId = attAppRec.Select(x => x.EmployeeId).SingleOrDefault();
                
                var attWF = db.Com_CommonApprovalWorkFlow.Join(
                        db.Com_CommonAssignData,
                        cap => cap.ApprovalWorkFlowId,
                        cas => cas.ApprovalWorkFlowId,
                        (cap, cas) => new { Com_CommonApprovalWorkFlow = cap, Com_CommonAssignData = cas }).Join(
                        db.ApprovalTypes,
                        cas => cas.Com_CommonAssignData.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cas, at) => new { cas.Com_CommonApprovalWorkFlow, cas.Com_CommonAssignData, ApprovalType = at }).Where(
                        x => x.ApprovalType.ApprovalTypeCode == "PCA" && x.Com_CommonAssignData.EmployeeId == attEmployeeId).Select(v => new
                        {
                            v.Com_CommonApprovalWorkFlow.ApprovePerson1,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson2,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson3
                        }).ToList();

                aprvPerson1Status = attAppRec.Select(x => x.ApprovePerson1Status).SingleOrDefault();
                aprvPerson2Status = attAppRec.Select(x => x.ApprovePerson2Status).SingleOrDefault();
                aprvPerson3Status = attAppRec.Select(x => x.ApprovePerson3Status).SingleOrDefault();
                relatedTableName = attAppRec.Select(x => x.RelatedTableName).SingleOrDefault();
                relatedColumnName = attAppRec.Select(x => x.RelatedColumnName).SingleOrDefault();
                ChangedValue = attAppRec.Select(x => x.TrueValue).SingleOrDefault();
                approvePerson1 = attWF.Select(x => x.ApprovePerson1).FirstOrDefault().ToString();
                approvePerson2 = attWF.Select(x => x.ApprovePerson2).FirstOrDefault().ToString();
                approvePerson3 = attWF.Select(x => x.ApprovePerson3).FirstOrDefault().ToString();

                if (attAppRec != null && attWF != null)
                {
                    var ad = db.POR_ChangeProfileDetails.Where(x => x.ID == ApprovalId).FirstOrDefault();

                    if (!approvePerson1.Equals("") && !approvePerson2.Equals("") && !approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            ad.ApprovePerson1Status = "Approved";
                            ad.ApprovePerson1 = Convert.ToInt64(approvePerson1);
                            ad.ApprovePerson1Remark = ApprovedReason;
                            ad.ApprovePerson2Status = "Pending";
                            ad.UpdatedUser = ModifiedUser;
                            ad.UpdatedDate = DateTime.Now;
                            db.SaveChanges();
                        }
                        else if (aprvPerson2Status == "Pending")
                        {
                            ad.ApprovePerson2Status = "Approved";
                            ad.ApprovePerson2 = Convert.ToInt64(approvePerson2);
                            ad.ApprovePerson2Remark = ApprovedReason;
                            ad.ApprovePerson3Status = "Pending";
                            ad.UpdatedUser = ModifiedUser;
                            ad.UpdatedDate = DateTime.Now;
                            db.SaveChanges();
                        }
                        else if (aprvPerson3Status == "Pending")
                        {
                            UpdateChanges(ApprovalId, relatedTableName, relatedColumnName, ChangedValue, ModifiedUser);

                            ad.Status = "Approved";
                            ad.ApprovePerson3 = Convert.ToInt64(approvePerson3);
                            ad.ApprovePerson3Remark = ApprovedReason;
                            ad.ApprovePerson3Status = "Approved";
                            ad.UpdatedUser = ModifiedUser;
                            ad.UpdatedDate = DateTime.Now;
                            db.SaveChanges();
                        }
                    }
                    else if (!approvePerson1.Equals("") && !approvePerson2.Equals("") && approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            ad.ApprovePerson1Status = "Approved";
                            ad.ApprovePerson1 = Convert.ToInt64(approvePerson1);
                            ad.ApprovePerson1Remark = ApprovedReason;
                            ad.ApprovePerson2Status = "Pending";
                            ad.UpdatedUser = ModifiedUser;
                            ad.UpdatedDate = DateTime.Now;
                            db.SaveChanges();
                        }
                        else if (aprvPerson2Status == "Pending")
                        {
                            UpdateChanges(ApprovalId, relatedTableName, relatedColumnName, ChangedValue, ModifiedUser);

                            ad.Status = "Approved";
                            ad.ApprovePerson2 = Convert.ToInt64(approvePerson2);
                            ad.ApprovePerson2Remark = ApprovedReason;
                            ad.ApprovePerson2Status = "Approved";
                            ad.UpdatedUser = ModifiedUser;
                            ad.UpdatedDate = DateTime.Now;
                            db.SaveChanges();
                        }
                    }
                    else if (!approvePerson1.Equals("") && approvePerson2.Equals("") && approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            UpdateChanges(ApprovalId, relatedTableName, relatedColumnName, ChangedValue, ModifiedUser);

                            ad.Status = "Approved";
                            ad.ApprovePerson1 = Convert.ToInt64(approvePerson1);
                            ad.ApprovePerson1Remark = ApprovedReason;
                            ad.ApprovePerson1Status = "Approved";
                            ad.UpdatedUser = ModifiedUser;
                            ad.UpdatedDate = DateTime.Now;
                            db.SaveChanges();
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

        public void UpdateChanges(int ApprovalId, string RelatedTableName, string RelatedColumnName, string Value, string UpdatedUser)
        {
            var approval = db.POR_ChangeProfileDetails.Where(x => x.ID == ApprovalId).FirstOrDefault();
            if(approval != null)
            {
                var emp = db.Employees.Where(x => x.EmployeeId == approval.EmployeeId).FirstOrDefault();
                if(emp != null)
                {
                    string previousImageFile = emp.Image;
                    var field = emp.GetType().GetProperty(RelatedColumnName); 
                    field.SetValue(emp, Value);
                    emp.UpdateUser = UpdatedUser;
                    emp.UpdateDate = DateTime.Now;
                    db.SaveChanges();

                    if(RelatedColumnName == "Image")
                    {
                        System.IO.File.Delete(ConfigurationManager.AppSettings["EmployeeImagePath"] + previousImageFile);
                        string filePath = approval.Value;
                        string newPath = ConfigurationManager.AppSettings["EmployeeImagePath"] + Value;
                        System.IO.File.Copy(filePath, newPath);
                    }
                }
            }
        }

        [HttpPost]
        public JsonResult ApprovePersonReject(int ApprovalId, string RejectReason, string ModifiedUser)
        {
            var result = false;
            try
            {
                long attEmployeeId = 0;
                string aprvPerson1Status = "";
                string aprvPerson2Status = "";
                string aprvPerson3Status = "";
                string approvePerson1 = "";
                string approvePerson2 = "";
                string approvePerson3 = "";
                

                var attAppRec = db.POR_ChangeProfileDetails.Where(x => x.ID == ApprovalId).Select(x => new
                {
                    x.EmployeeId,
                    x.RelatedTableName,
                    x.RelatedColumnName,
                    x.Value,
                    x.ApprovePerson1Status,
                    x.ApprovePerson2Status,
                    x.ApprovePerson3Status
                }).ToList();

                attEmployeeId = attAppRec.Select(x => x.EmployeeId).SingleOrDefault();
                
                var attWF = db.Com_CommonApprovalWorkFlow.Join(
                        db.Com_CommonAssignData,
                        cap => cap.ApprovalWorkFlowId,
                        cas => cas.ApprovalWorkFlowId,
                        (cap, cas) => new { Com_CommonApprovalWorkFlow = cap, Com_CommonAssignData = cas }).Join(
                        db.ApprovalTypes,
                        cas => cas.Com_CommonAssignData.ApprovalTypeID,
                        at => at.ApprovalTypeID,
                        (cas, at) => new { cas.Com_CommonApprovalWorkFlow, cas.Com_CommonAssignData, ApprovalType = at }).Where(
                        x => x.ApprovalType.ApprovalTypeCode == "PCA" && x.Com_CommonAssignData.EmployeeId == attEmployeeId).Select(v => new
                        {
                            v.Com_CommonApprovalWorkFlow.ApprovePerson1,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson2,
                            v.Com_CommonApprovalWorkFlow.ApprovePerson3
                        }).ToList();

                aprvPerson1Status = attAppRec.Select(x => x.ApprovePerson1Status).SingleOrDefault();
                aprvPerson2Status = attAppRec.Select(x => x.ApprovePerson2Status).SingleOrDefault();
                aprvPerson3Status = attAppRec.Select(x => x.ApprovePerson3Status).SingleOrDefault();
                approvePerson1 = attWF.Select(x => x.ApprovePerson1).FirstOrDefault().ToString();
                approvePerson2 = attWF.Select(x => x.ApprovePerson2).FirstOrDefault().ToString();
                approvePerson3 = attWF.Select(x => x.ApprovePerson3).FirstOrDefault().ToString();

                if (attAppRec != null && attWF != null)
                {
                    var ad = db.POR_ChangeProfileDetails.Where(x => x.ID == ApprovalId).FirstOrDefault();

                    if (!approvePerson1.Equals("") && !approvePerson2.Equals("") && !approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            ad.ApprovePerson1Status = "Rejected";
                            ad.ApprovePerson1 = Convert.ToInt64(approvePerson1);
                            ad.ApprovePerson1Remark = RejectReason;
                            ad.Status = "Rejected";
                            ad.RejectedBy = Convert.ToInt64(approvePerson1);
                            ad.RejectedReason = RejectReason;
                            ad.UpdatedUser = ModifiedUser;
                            ad.UpdatedDate = DateTime.Now;
                            db.SaveChanges();
                        }
                        else if (aprvPerson2Status == "Pending")
                        {
                            ad.ApprovePerson2Status = "Rejected";
                            ad.ApprovePerson2 = Convert.ToInt64(approvePerson2);
                            ad.ApprovePerson2Remark = RejectReason;
                            ad.Status = "Rejected";
                            ad.RejectedBy = Convert.ToInt64(approvePerson2);
                            ad.RejectedReason = RejectReason;
                            ad.UpdatedUser = ModifiedUser;
                            ad.UpdatedDate = DateTime.Now;
                            db.SaveChanges();
                        }
                        else if (aprvPerson3Status == "Pending")
                        {
                            ad.ApprovePerson1Status = "Rejected";
                            ad.ApprovePerson3 = Convert.ToInt64(approvePerson3);
                            ad.ApprovePerson3Remark = RejectReason;
                            ad.Status = "Rejected";
                            ad.RejectedBy = Convert.ToInt64(approvePerson3);
                            ad.RejectedReason = RejectReason;
                            ad.UpdatedUser = ModifiedUser;
                            ad.UpdatedDate = DateTime.Now;
                            db.SaveChanges();
                        }
                    }
                    else if (!approvePerson1.Equals("") && !approvePerson2.Equals("") && approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            ad.ApprovePerson1Status = "Rejected";
                            ad.ApprovePerson1 = Convert.ToInt64(approvePerson1);
                            ad.ApprovePerson1Remark = RejectReason;
                            ad.Status = "Rejected";
                            ad.RejectedBy = Convert.ToInt64(approvePerson1);
                            ad.RejectedReason = RejectReason;
                            ad.UpdatedUser = ModifiedUser;
                            ad.UpdatedDate = DateTime.Now;
                            db.SaveChanges();
                        }
                        else if (aprvPerson2Status == "Pending")
                        {
                            ad.ApprovePerson2Status = "Rejected";
                            ad.ApprovePerson2 = Convert.ToInt64(approvePerson2);
                            ad.ApprovePerson2Remark = RejectReason;
                            ad.Status = "Rejected";
                            ad.RejectedBy = Convert.ToInt64(approvePerson2);
                            ad.RejectedReason = RejectReason;
                            ad.UpdatedUser = ModifiedUser;
                            ad.UpdatedDate = DateTime.Now;
                            db.SaveChanges();
                        }
                    }
                    else if (!approvePerson1.Equals("") && approvePerson2.Equals("") && approvePerson3.Equals(""))
                    {
                        if (aprvPerson1Status == "Pending")
                        {
                            ad.ApprovePerson1Status = "Rejected";
                            ad.ApprovePerson1 = Convert.ToInt64(approvePerson1);
                            ad.ApprovePerson1Remark = RejectReason;
                            ad.Status = "Rejected";
                            ad.RejectedBy = Convert.ToInt64(approvePerson1);
                            ad.RejectedReason = RejectReason;
                            ad.UpdatedUser = ModifiedUser;
                            ad.UpdatedDate = DateTime.Now;
                            db.SaveChanges();
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
    }
}