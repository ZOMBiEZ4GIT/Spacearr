using System;
using Microsoft.AspNetCore.Mvc;

namespace Spacearr.Http.REST.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RestPostByIdAttribute : HttpPostAttribute
    {
    }
}
