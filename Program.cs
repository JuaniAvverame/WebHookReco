using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);
var connectionString = "Server=192.168.56.1;Database=ADMCONACT;User Id=xges;Password=lalo7;TrustServerCertificate=True;";
// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();


app.MapGet("/webhook", (HttpRequest request) =>
{
    var mode = request.Query["hub.mode"].ToString();
    var token = request.Query["hub.verify_token"].ToString();
    var challenge = request.Query["hub.challenge"].ToString();

    if (mode == "subscribe" && token == "MI_TOKEN")
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

    var json = Newtonsoft.Json.Linq.JObject.Parse(body);

    var cambios = json["entry"]?[0]?["changes"];
    if (cambios == null)
        return Results.Ok();

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
            string tipoRespuesta = "";

            // CASO 1: botón simple
            if (tipo == "button")
            {
                idBoton = msg["button"]?["payload"]?.ToString();
                textoBoton = msg["button"]?["text"]?.ToString();
                tipoRespuesta = "BOTON";
            }

            // CASO 2: interactive/button_reply
            if (tipo == "interactive")
            {
                var interactiveType = msg["interactive"]?["type"]?.ToString();

                if (interactiveType == "button_reply")
                {
                    idBoton = msg["interactive"]?["button_reply"]?["id"]?.ToString();
                    textoBoton = msg["interactive"]?["button_reply"]?["title"]?.ToString();
                    tipoRespuesta = "BOTON";
                }
            }

            if (!string.IsNullOrWhiteSpace(idBoton) || !string.IsNullOrWhiteSpace(textoBoton))
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    var sql = @"INSERT INTO WhatsAppRespuestas
                                (TelefonoCliente, NombreCliente, TextoMensaje, TipoRespuesta, ValorBoton, Fecha)
                                VALUES (@tel, @nom, @txt, @tipo, @val, GETDATE())";

                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@tel", (object?)telefono ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@nom", DBNull.Value);
                    cmd.Parameters.AddWithValue("@txt", (object?)textoBoton ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tipo", tipoRespuesta);
                    cmd.Parameters.AddWithValue("@val", (object?)idBoton ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }

                Console.WriteLine("GUARDADO EN WhatsAppRespuestas");
                Console.WriteLine($"Telefono: {telefono} | Texto: {textoBoton} | Valor: {idBoton}");

                if ((idBoton ?? "").Trim().Equals("Recibir Estado", StringComparison.OrdinalIgnoreCase) ||
                    (textoBoton ?? "").Trim().Equals("Recibir Estado", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("CONFIRMÓ RECIBIR ESTADO");
                }
            }
        }
    }

    return Results.Ok();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{  
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
