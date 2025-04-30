using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CRUD_OperationByMeUsingJqueryAjaxMvc.Models.Shared
{
    public class ConvertDateToAll
    {
        public DateTime GetDateToAll(string Date)
        {
            string[] dateArr = Date.Split('/');
            int year = Convert.ToInt32(dateArr[2]);
            int month = Convert.ToInt32(dateArr[0]);
            int day = Convert.ToInt32(dateArr[1]);
            DateTime date = new DateTime(year, month, day);
            return date;
        }
    }
}