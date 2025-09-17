#region Using directives
using System;
using System.Linq;
using System.Net.Mime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
#endregion

namespace BlazoriseLicenseProxy;

public class Program
{
    public static void Main( string[] args )
    {
        var builder = WebApplication.CreateBuilder( args );

        var cfg = builder.Configuration;

        var allowedOrigins = cfg.GetSection( "AllowedOrigins" ).Get<string[]>() ?? Array.Empty<string>();

        builder.Services.AddCors( opts =>
        {
            opts.AddPolicy( "WasmClient", p => p
                .WithOrigins( allowedOrigins )
                .AllowAnyHeader()
                .AllowAnyMethod() );
        } );

        builder.Services.AddRateLimiter( o =>
        {
            o.AddFixedWindowLimiter( "fixed", options =>
            {
                options.PermitLimit = 60;
                options.Window = TimeSpan.FromMinutes( 1 );
                options.QueueLimit = 0;
            } );
        } );

        // Single, shared token (keep in secrets/KeyVault; not in repo)
        var productToken = cfg["Licensing:ProductToken"] ?? throw new InvalidOperationException( "Missing Licensing:ProductToken" );

        var app = builder.Build();

        if ( !app.Environment.IsDevelopment() )
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseCors( "WasmClient" );
        app.UseRateLimiter();

        // Health check endpoint
        app.MapGet( "/healthz", () => Results.Text( "ok", MediaTypeNames.Text.Plain ) );

        // Licensing token endpoint
        app.MapGet( "/licensing/token", ( IConfiguration cfg, HttpContext ctx ) =>
        {
            const string RequiredHeader = "X-Blazorise-Client";

            if ( !ctx.Request.Headers.TryGetValue( RequiredHeader, out var hv ) || hv != "1" )
                return Results.StatusCode( StatusCodes.Status403Forbidden );

            var origin = ctx.Request.Headers.Origin.ToString();

            if ( string.IsNullOrEmpty( origin ) || !allowedOrigins.Contains( origin, StringComparer.OrdinalIgnoreCase ) )
                return Results.StatusCode( StatusCodes.Status403Forbidden );

            ctx.Response.Headers["Cache-Control"] = "no-store";
            ctx.Response.Headers["Pragma"] = "no-cache";
            ctx.Response.Headers["Vary"] = "Origin";

            return Results.Json( new { token = productToken }, contentType: MediaTypeNames.Application.Json );
        } )
        .RequireCors( "WasmClient" )
        .RequireRateLimiting( "fixed" );

        app.Run();
    }
}
