using System;
using System.Collections.Generic;
using System.Text;

namespace Hermes.Application.DTOs.User
{
    public class UserVerificationCodeRequest
    {
        public int UserId { get; set; }
        public int Code { get; set; }
    }
}
