using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CRUD_OperationByMeUsingJqueryAjaxMvc.Models.Shared
{
    public class ValidPayroll
    {
        static Dhigurah_DBEntities db = new Dhigurah_DBEntities();

        public static bool IsPostedPayperiod(long EmployeeId, DateTime Date)
        {
            bool isPosted = false;
            var payPeriod = db.Pay_EmplyoeePay.Where(x => x.EmployeeId == EmployeeId).Join(
                    db.Pay_PayPeriod.Where(x => x.BeginDate >= Date && x.EndDate <= Date && x.Posted == true),
                    pem => pem.PayPeriodCategoryId,
                    pp => pp.PayPeriodCategoryId,
                    (pem, pp) => new { Pay_EmplyoeePay = pem, Pay_PayPeriod = pp }).Select(x => new { x.Pay_PayPeriod.PayPeriodId }).FirstOrDefault();

            if(payPeriod != null)
            {
                isPosted = true;
            }

            return isPosted;
        }
    }
}