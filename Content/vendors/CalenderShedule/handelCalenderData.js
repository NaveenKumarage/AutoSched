var globleId = "";
var currentUpdateEvent;
var addStartDate;
var addEndDate;
var globalAllDay;

$(function () {
    $.ajax({

        type: "POST",
        url: "Calender.aspx/getall",
        data: "{}",
        contentType: "application/json; charset=utf-8",
        //username: 'mlm',
        //password: 'asdf1234',
        dataType: "json",
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

        var dayType = allType[i].DayType;
        var dayTypeId = allType[i].DayTypeId;

        htmlDragable += " <div class='fc-event ui-draggable ui-draggable-handle " + dayTypeId + "' value=\"" + dayType + "\"  ondragstart=callDraged(this)  style='background-color:" + allType[i].color + "; border:1px solid " + allType[i].color + "; color: " + allType[i].textColor + ";font-weight:bold; ' id=" + dayTypeId + ">" + dayType;
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

