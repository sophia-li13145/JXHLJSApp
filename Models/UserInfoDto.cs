
namespace JXHLJSApp.Models;

    public class UserInfoDto
    {
        public string? id { get; set; }
        public string? username { get; set; }   // 登录账号
        public string? realname { get; set; }   // 真实姓名
        public string? phone { get; set; }
        public string? email { get; set; }
        public string? roleCode { get; set; }
        public string? roleName { get; set; }
        public string? workNumber { get; set; }
        public string? factoryName { get; set; }
        public string? workshopName { get; set; }
        public string? teamName { get; set; }
        public string? shiftName { get; set; }
        public string? loginType { get; set; }
    }

