var builder = WebApplication.CreateBuilder(args);
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHttpClient("AuthClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5002/");
});

builder.Services.AddCors();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
});

var app = builder.Build();
app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.Use(async (context, next) =>
{

    if (context.Request.Path.StartsWithSegments("/products"))
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var token))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Authorization header missing");
            return;
        }

        var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
        var client = httpClientFactory.CreateClient("AuthClient");

        var tokenValue = token.ToString().Replace("Bearer ", "");
        var response = await client.PostAsJsonAsync("validate", new { Token = tokenValue });

        if (!response.IsSuccessStatusCode)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid token");
            return;
        }
    }

    // Si pasó la validación o no es /products, continúa con la petición
    await next();
});
app.MapReverseProxy();
app.Run();