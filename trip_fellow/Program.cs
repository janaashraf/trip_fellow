using Microsoft.Data.Sqlite;
using Dapper;
using System.Text.Json;
using System.Data;

var builder = WebApplication.CreateBuilder(args);
var connectionString = "Data Source=database.db;";
builder.Services.AddSingleton<IDbConnection>(_ => new SqliteConnection(connectionString));
var app = builder.Build();

using (var connection = new SqliteConnection(connectionString))
{
    connection.Open();
}

app.MapGet("/", async context =>
{
    context.Response.Headers["Content-Type"] = "text/html";
    await context.Response.SendFileAsync("wwwroot/index.html");
});

app.UseStaticFiles();

// endpoint to fetch recommendations
app.MapGet("/recommendations", async (HttpContext context, IDbConnection db) =>
{
    var recommendations = (await db.QueryAsync<Recommendation>("SELECT * FROM Recommendations")).ToList();
    return Results.Ok(recommendations);
});

app.MapPost("/add-recommendation", async (HttpContext context, IDbConnection db) =>
{
    var name = context.Request.Form["name"].ToString().Trim();
    var imageFile = context.Request.Form.Files["image"];

    if (string.IsNullOrEmpty(name))
    {
        return Results.BadRequest("Name cannot be empty");
    }
    if (imageFile == null || imageFile.Length == 0)
    {
        return Results.BadRequest("Image file is required");
    }

    // Check file extension
    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
    var fileExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
    if (!allowedExtensions.Contains(fileExtension))
    {
        return Results.BadRequest("Invalid file format. Only JPG, JPEG, PNG, and GIF images are allowed.");
    }

    var imageUrl = "";
    if (imageFile != null && imageFile.Length > 0)
    {
        // Generate a unique filename for the image
        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(uploadPath);
        var filePath = Path.Combine(uploadPath, fileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await imageFile.CopyToAsync(stream);
        }
        imageUrl = Path.Combine("/uploads", fileName);
    }

    var result = await db.QuerySingleOrDefaultAsync<int>("INSERT INTO Recommendations (Name, ImageUrl) VALUES (@Name, @ImageUrl); SELECT last_insert_rowid();", new { Name = name, ImageUrl = imageUrl });
    var newRecommendationId = result;
    var recommendationData = new
    {
        id = newRecommendationId,
        name = name,
        imageUrl = imageUrl
    };
    return Results.Ok(recommendationData);
});

//delete recommendation
app.MapDelete("/delete-recommendation", async (HttpContext context, IDbConnection db) =>
{
    var name = context.Request.Form["name"].ToString();
    await db.ExecuteAsync("DELETE FROM Recommendations WHERE Name = @Name", new { Name = name });
    return Results.Ok();
});
app.Run();
public class Recommendation
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string ImageUrl { get; set; }
}