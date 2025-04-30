$(document).ready(function () {
    //setInterval(function () {
    //    LoadNotificationList();
    //}, 15000)

    LoadNotificationList();
})

function LoadNotificationList() {
    $.ajax({
        type: "Post",
        url: "/Home/GetNotifications",
        data: { "EmployeeId": $('#EmpId').val()},
        success: function (result) {
            if (result.success) {
                $('#notList').empty();
                $('#spanCount').text(result.count);
                $.each(result.dataList, function (key, value) {
                    //alert(value[0].PaperName);
                    var SetData = $("#notList");

                    var Data = `
                             <li class="nav-item alert-border-${value.Color}">
                              <div class="d-flex"  style="position:relative">
                                <img src="/Content/images/notification/close.png" class="alert-close" />
                                <div style="display:grid;place-content:center;grid-template-columns:30px 1fr">
                                  <i class="${value.Icon} fa-2x alert-message-${value.Color}"></i>
                                </div>
                                <div style="display:grid" class="pl-2 pr-3 pb-0 pt-0">
                                  <p class="p-1 mb-0 alert-message-${value.Color}" style="font-size:14px;font-weight:bold;">${value.NotificationType}</p>
                                  <a  onclick="SelectNotification(${value.NotificationId})" class="dropdown-item">
                                    <span class="message alert-message-${value.Color}">${value.Content}</span>
                                    <span class="message alert-message-${value.Color}" style="font-weight:bold">${value.CreatedDate}</span>
                                  </a>
                                </div>
                              </div>
                            </li>
                            `

                    SetData.append(Data);
                });
            } 
        }
    })
}

//To redirect when notification click
function SelectNotification(NotificationId) {
    $.ajax({
        type: "Post",
        url: "/Home/SelectNotification",
        data: { "NotificationId": NotificationId },
        success: function (result) {
            if (result.success) {
                window.location.href = "/" + result.controller + "/" + result.method;
            }
        }
    })
}