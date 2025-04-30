var globleId = "";
var currentUpdateEvent;
var addStartDate;
var addEndDate;
var globalAllDay;
var globalAllCell;
var calenderDateDetails ="";



$(function () {
    $.ajax({

        type: "POST",
        url: "EmployeeShiftSheduler.aspx/getall",
        data: "{}",
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        //username: 'mlm',
        //password: 'asdf1234',
        async: false,
        success: fnsuccesscallback
        //error: fnerrorcallback
    });
});


function fnsuccesscallback(data) {

    var allType = JSON.parse(data.d);
    var htmlDragable = "";
    var k = 1;
    for (var i = 0; i < allType.length ; i++) {

        var dayType = allType[i].ShiftSetupName;
        var dayTypeId = allType[i].ShiftSetupId;

        htmlDragable += " <div class='fc-event ui-draggable ui-draggable-handle " + dayTypeId + "' value=\"" + dayType + "\"  ondragstart=callDraged(this)  style=font-weight:bold;vertical-align: middle; id=" + dayTypeId + "><span><input runat=server type=radio  style=" + "vertical-align: middle" + " id=" + k + "  name=dayTypeList value=" + dayTypeId + "></span>" + dayType;
       // htmlDragable += " <input runat=server type=radio id=" + k + " name=dayTypeList value=" + dayTypeId + ">";
        htmlDragable += " </div>";
        k++;
    }
    $('#dragabaleDiv').html(htmlDragable);


    //$("#calendar").fullCalendar({
    //    dayRender: function (date, cell) {
    //        cell.css("background-color", "red");
    //    }
    //});

}

