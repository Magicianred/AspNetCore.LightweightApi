﻿using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace AspNetCore.LightweightApi
{
    public interface IEndpointHandler
    {
        public Task Handle(HttpContext context);
    }

    public interface IEndpointHandler<TOutput>
    {
        public Task<TOutput> Handle(HttpContext context);
    }
}
