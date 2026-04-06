using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var app = builder.Build();

app.MapGet("/webhook", (HttpRequest request) =>
{
    var mode = request.Query["hub.mode"].ToString();
    var token = request.Query["hub.verify_token"].ToString();
    var challenge = request.Query["hub.challenge"].ToString();

    var verifyToken = builder.Configuration["VERIFY_TOKEN"];

    if (mode == "subscribe" && token == verifyToken)
    {
        return Results.Text(challenge, "text/plain");
    }

    return Results.StatusCode(403);
});

app.MapPost("/webhook", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();

    Console.WriteLine("POST RECIBIDO");
    Console.WriteLine(body);

    var json = JObject.Parse(body);

    var cambios = json["entry"]?[0]?["changes"];
    if (cambios == null)
        return Results.Ok();

    var supabaseUrl = builder.Configuration["SUPABASE_URL"];
    var supabaseKey = builder.Configuration["SUPABASE_KEY"];

    foreach (var change in cambios)
    {
        var value = change["value"];
        var mensajes = value?["messages"];
        if (mensajes == null)
            continue;

        foreach (var msg in mensajes)
        {
            var telefono = msg["from"]?.ToString();
            var tipo = msg["type"]?.ToString();

            string? idBoton = null;
            string? textoBoton = null;

            if (tipo == "button")
            {
                idBoton = msg["button"]?["payload"]?.ToString();
                textoBoton = msg["button"]?["text"]?.ToString();
            }

            if (tipo == "interactive")
            {
                var interactiveType = msg["interactive"]?["type"]?.ToString();
                if (interactiveType == "button_reply")
                {
                    idBoton = msg["interactive"]?["button_reply"]?["id"]?.ToString();
                    textoBoton = msg["interactive"]?["button_reply"]?["title"]?.ToString();
                }
            }

            if (!string.IsNullOrWhiteSpace(idBoton) || !string.IsNullOrWhiteSpace(textoBoton))
            {
                try
                {
                    using var client = new HttpClient();

                    client.DefaultRequestHeaders.Add("apikey", supabaseKey);
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");

                    var url = $"{supabaseUrl}/rest/v1/whatsapprespuestas";

                    var data = new
                    {
                        telefono = telefono,
                        texto = textoBoton,
                        valor = idBoton
                    };

                    var payload = JsonConvert.SerializeObject(data);
                    var content = new StringContent(payload, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(url, content);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("ERROR SUPABASE:");
                        Console.WriteLine(responseBody);
                    }
                    else
                    {
                        Console.WriteLine("GUARDADO EN SUPABASE");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR AL GUARDAR EN SUPABASE:");
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }

    return Results.Ok();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();
app.Run();