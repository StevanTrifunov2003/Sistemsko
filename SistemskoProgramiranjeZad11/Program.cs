using SistemskoProgramiranjeZad11;
using System;
using System.Drawing.Imaging;
using System.Drawing;
using System.Net;
using System.Diagnostics;
namespace SistemskoProgramiranjeZad11
{
    class Program
    {     
        static void Main(string[] args)
        {
            const string rootFolder = "C:\\Images\\";
            const string baseUrl = "http://localhost:5050/";

            Console.WriteLine("Web server for converting a picture to grayscale is running...");
            Console.WriteLine($"Listening on {baseUrl}");

            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(baseUrl);

            try
            {
                listener.Start();

                while (true)
                {
                    HttpListenerContext context = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(new WaitCallback(HandleRequestAsync), new object[] { context, rootFolder });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                listener.Close();
            }
        }

        static void HandleRequestAsync(object state)
        {
            if (state is object[] parameters && parameters.Length == 2)
            {
                HttpListenerContext context = (HttpListenerContext)parameters[0];
                string rootFolder = (string)parameters[1];

                string requestUrl = context.Request.Url.AbsolutePath;

                try
                {
                    if (!requestUrl.StartsWith("/"))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        context.Response.Close();
                        Console.WriteLine($"Bad request: {requestUrl}");
                        return;
                    }

                    string fileName = requestUrl.Substring(1); // Preskacemo prvi karakter ("/")
                    string filePath = Path.Combine(rootFolder, fileName);

                    // Provera da li je slika vec kesirana
                    Bitmap cachedImage = GetCachedImage(filePath);

                    if (cachedImage != null)
                    {
                        SendImageResponse(context, cachedImage);
                        Console.WriteLine($"Cached image sent: {fileName}");
                        return;
                    }

                    if (!File.Exists(filePath))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        context.Response.Close();
                        Console.WriteLine($"File not found: {fileName}");
                        return;
                    }

                    // Konvertovanje slike u crno-beli format
                    using (var originalImage = new Bitmap(filePath))
                    using (var grayscaleImage = new Bitmap(originalImage.Width, originalImage.Height))
                    {
                        for (int y = 0; y < originalImage.Height; y++)
                        {
                            for (int x = 0; x < originalImage.Width; x++)
                            {
                                Color pixelColor = originalImage.GetPixel(x, y);
                                int grayscale = (int)(pixelColor.R * 0.3 + pixelColor.G * 0.59 + pixelColor.B * 0.11);
                                Color newColor = Color.FromArgb(grayscale, grayscale, grayscale);
                                grayscaleImage.SetPixel(x, y, newColor);
                            }
                        }
                        // Cuvanje konvertovane slike u kesu
                        AddImageToCache(filePath, grayscaleImage, new TimeSpan(0, 1, 0));

                        SendImageResponse(context, grayscaleImage);
                        Console.WriteLine($"Converted image sent: {fileName}");
                    }
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.Close();
                    Console.WriteLine($"Error processing request for {requestUrl}: {ex.Message}");
                }
            }
        }
        static void SendImageResponse(HttpListenerContext context, Bitmap image)
        {
            // Cuvanje konvertovane slike u memoriji
            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Jpeg);
                byte[] imageBytes = ms.ToArray();

                // Slanje odgovora
                context.Response.ContentType = "image/jpeg";
                context.Response.OutputStream.Write(imageBytes, 0, imageBytes.Length);
                context.Response.Close();
            }
        }

        // Dictionary za kesiranje slika
        private static readonly Dictionary<string, CachedImage> imageCache = new Dictionary<string, CachedImage>();

        // Struktura koja predstavlja kesiranu sliku sa vremenom isteka
        private struct CachedImage
        {
            public Bitmap Image;
            public DateTime ExpirationTime;
        }

        static Bitmap GetCachedImage(string filePath)
        {
            lock (imageCache)
            {
                if (imageCache.ContainsKey(filePath))
                {                  
                    if (DateTime.Now > imageCache[filePath].ExpirationTime) 
                    {
                        return imageCache[filePath].Image; 
                    }
                    else
                    {
                        imageCache.Remove(filePath);
                    }
                }
                return null;
            }
        }

        static void AddImageToCache(string filePath, Bitmap image, TimeSpan expirationTime)
        {
            lock (imageCache)
            {              
                imageCache[filePath] = new CachedImage
                {
                    Image = image,
                    ExpirationTime = DateTime.Now.Add(expirationTime)
                };
            }
        }
    }
}