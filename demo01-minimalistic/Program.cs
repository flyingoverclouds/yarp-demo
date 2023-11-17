var builder = WebApplication.CreateBuilder(args);


//builder.Services.AddReverseProxy()
//    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));


var app = builder.Build();

app.MapGet("/", () => "Hello World!"); // inutile, mais c'est pour la démo :)

//app.MapReverseProxy();

app.Run();
