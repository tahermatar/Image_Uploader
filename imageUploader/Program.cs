using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

ImageRepository imageRepository = new ImageRepository();
// Endpoint for the form submission
app.MapGet("/", async (context) =>
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "index.html");
    await context.Response.WriteAsync(File.ReadAllText(path));
});

app.MapGet("/picture/{imageId}", async (HttpContext context) =>
{
    var imageId = context.Request.RouteValues["imageId"]?.ToString();
    var image = imageRepository.GetImageById(imageId);

    if (string.IsNullOrEmpty(imageId) || image == null)
    {
        context.Response.StatusCode = 404;
        return context.Response.WriteAsync("Image not found.");
    }

    byte[] imageBytes = await File.ReadAllBytesAsync(image.FilePath);
    string imageBase64Data = Convert.ToBase64String(imageBytes);
    var response = $@"<!DOCTYPE html>
                    <html>
                        <head>
                            <meta charset=""utf-8"" />
                            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                            <title>Image Uploader</title>
                            <link href=""https://fastly.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css"" rel=""stylesheet"" integrity=""sha384-9ndCyUaIbzAi2FUVXJi0CjmCapSmO7SnpJef0486qhLnuZ2cdeRhO02iuK6FUUVM"" crossorigin=""anonymous"">
                        </head>
                        <body>
                            <div class=""container card shadow p-0 d-flex justify-content-center mt-5"" style=""width: 25rem;"">
                                <img src=""data:image/png;base64,{imageBase64Data}"" alt=""{image.Title}"" class=""card-img-top"" style=""width: 100%;"" >
                                <div class=""card-body"">
                                    <h4 class=""card-title"">Title:</h4>
                                    <h5 class=""card-title"">{image.Title}</h5>
                                </div>
                                <div class=""card-body"">
                                    <a href=""/"" class=""btn btn-primary"">Back to form</a>
                                </div>
                            </div>
                        </body>
                    </html>";

    context.Response.ContentType = "text/html";
    return context.Response.WriteAsync(response);
});

app.MapPost("/", async (HttpContext context) =>
{
    var title = context.Request.Form["title"];
    var file = context.Request.Form.Files["image"];

    // Validate the input
    if (string.IsNullOrEmpty(title))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Title is required.");
        return;
    }

    if (file == null || file.Length == 0)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Image file is required.");
        return;
    }

    var allowedExtensions = new[] { ".jpeg", ".jpg", ".png", ".gif" };
    var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (!allowedExtensions.Contains(fileExtension))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Invalid file format. Only jpeg, png, or gif are allowed.");
        return;
    }

    // Save the file
    var uniqueId = Guid.NewGuid().ToString();
    var filePath = Path.Combine("Images", uniqueId + fileExtension);
    Directory.CreateDirectory("Images");
    using (var fileStream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(fileStream);
    }

    // Store the image information in JSON
    var image = new ImageInfo
    {
        Id = uniqueId,
        Title = title,
        FilePath = filePath
    };

    imageRepository.AddImage(image);

    // Redirect to the page with unique id
    context.Response.Redirect($"/picture/{uniqueId}");
});

// Endpoint for displaying the image


app.Run();

public class ImageInfo
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string FilePath { get; set; }
}

public class ImageRepository
{
    private const string StorageFilePath = "imageInfo.json";
    private List<ImageInfo> images;

    public ImageRepository()
    {
        LoadImages();
    }

    public void AddImage(ImageInfo image)
    {
        images.Add(image);
        SaveImages();
    }

    public ImageInfo GetImageById(string imageId)
    {
        return images.Find(image => image.Id == imageId);
    }

    private void LoadImages()
    {
        if (File.Exists(StorageFilePath))
        {
            var json = File.ReadAllText(StorageFilePath);
            images = JsonSerializer.Deserialize<List<ImageInfo>>(json);
        }
        else
        {
            images = new List<ImageInfo>();
        }
    }

    private void SaveImages()
    {
        var json = JsonSerializer.Serialize(images);
        File.WriteAllText(StorageFilePath, json);
    }
}