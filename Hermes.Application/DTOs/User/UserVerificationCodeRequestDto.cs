using System;
using System.Collections.Generic;
using System.Text;

namespace Hermes.Application.DTOs.User
{
    public class UserVerificationCodeRequestDto
    {
        public int UserId { get; set; }
        public int Code { get; set; }
    }
}
