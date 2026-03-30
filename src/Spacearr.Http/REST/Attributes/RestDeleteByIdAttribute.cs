using System;
using Microsoft.AspNetCore.Mvc;

namespace Spacearr.Http.REST.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RestDeleteByIdAttribute : HttpDeleteAttribute
    {
        public RestDeleteByIdAttribute()
            : base("{id:int}")
        {
        }
    }
}
