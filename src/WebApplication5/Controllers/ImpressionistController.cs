using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Http;
using System.IO;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Mvc.Rendering;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Web;

// For more information on enabling MVC for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace WebApplication5.Controllers
{
    public class ImpressionistController : Controller
    {
        private IHostingEnvironment _environment;
        public ImpressionistController(IHostingEnvironment environment)
        {
            _environment = environment;
        }

        // GET: /<controller>/
        public IActionResult Index()
        {
            return View();
        }

        private static IEnumerable<string> EffectNames = new List<string> {
            "Bas Relief",
            "Chalk And Charcoal",
            "Charcoal",
            "Chrome",
            "Colored Pencil",
            "Cutout",
            "Diffuse Glow",
            "Fresco",
            //"Glass", // Requires additional texture files to be uploaded to the Azure Function
            "Glowing Edges",
            "Grain",
            "Graphic Pen",
            "Halftone Screen",
            "Mosaic",
            "Neon Glow",
            "Note Paper",
            "Palette Knife",
            "Patchwork",
            "Photocopy",
            "Plastic Wrap",
            "Poster Edges",
            "Ripple",
            "Spatter",
            "Sponge",
            "Sprayed Strokes",
            "Stained Glass",
            "Watercolor"
        };


        // GET: /<controller>/FromSample?sampleName=sample1.jpg&effectName=Ripple
        public async Task<IActionResult> FromSample(string sampleName)
        {
            string sourceImage = Path.Combine(_environment.WebRootPath, "images", sampleName);

            await RunImpressionistAndBindResults(sourceImage, 2);

            return View("Index");
        }

        // POST: /<controller>/PostImage?effectName=Ripple
        [HttpPost]
        public async Task<IActionResult> PostImage(ICollection<IFormFile> files)
        {
            var uploads = Path.Combine(_environment.WebRootPath, "uploads");
            var formFile = files.ElementAt(0);

            string fileName = "original.jpg";
            string sourceImage = Path.Combine(_environment.WebRootPath, "images", fileName);
            formFile.SaveAs(sourceImage);

            await RunImpressionistAndBindResults(sourceImage, 2);

            return View("Index");
        }

        private async Task RunImpressionistAndBindResults(string sourceImage, int numberOfRuns)
        {
            var effects = SelectRandomEffects(numberOfRuns);

            for (int i = 0; i < numberOfRuns; i++)
            {
                await RunImpressionist(sourceImage, $"masterpiece{i}.jpg", $"\"{effects.ElementAt(i)}\"");
                this.ViewData[$"ResultImage{i}"] = $"masterpiece{i}.jpg";
                this.ViewData[$"ResultFilterName{i}"] = effects.ElementAt(i);
            }
            this.ViewData["OriginalImage"] = Path.GetFileName(sourceImage);
        }

        private async Task RunImpressionist(string sourceImage, string saveAsName, string effectName)
        {
            var result = UploadImage(sourceImage, effectName);

            string resultFile = Path.Combine(_environment.WebRootPath, "images", saveAsName);
            using (FileStream fs = new FileStream(resultFile, FileMode.Create))
            {
                await (result.Content as StreamContent).CopyToAsync(fs);
            }
        }


        private void CleanupDir()
        {
            DirectoryInfo di = new DirectoryInfo(_environment.WebRootPath);
            FileInfo[] files = di.GetFiles("*.jpg")
                             .Where(p => p.Extension == ".jpg").ToArray();

            foreach (FileInfo file in files)
            {
                System.IO.File.Delete(file.FullName);
            }
        }


        private HttpResponseMessage UploadImage(string file, string effectName)
        {
            effectName = Uri.EscapeUriString(effectName);

            using (var client = new HttpClient())
            {
                var requestContent = new MultipartFormDataContent();
                //    here you can specify boundary if you need---^
                var imageContent = new ByteArrayContent(ImageToByteArray(file));
                imageContent.Headers.ContentType =
                    MediaTypeHeaderValue.Parse("image/jpeg");

                requestContent.Add(imageContent, "image", "image.jpg");
                string url = "https://impressionist-bot.azurewebsites.net/api/commandline-http?code=xv7o09ft3zoh7g9dbnh4cxr1l6qxpd1s0xn7yfy2jfo1xajorydr1h8aq89dxsjs9jjdcxr&effectName=" + effectName;
                //string url = "https://impressionist-bot.azurewebsites.net/api/commandline-http?code=xv7o09ft3zoh7g9dbnh4cxr1l6qxpd1s0xn7yfy2jfo1xajorydr1h8aq89dxsjs9jjdcxr";
                System.Diagnostics.Debug.WriteLine("Sending request to: " + url);
                var result = client.PostAsync(url, requestContent).Result;

                return result;
            }
        }

        private static byte[] ImageToByteArray(string imageLocation)
        {
            byte[] imageData = null;
            FileInfo fileInfo = new FileInfo(imageLocation);
            long imageFileLength = fileInfo.Length;

            using (FileStream fs = new FileStream(imageLocation, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                imageData = br.ReadBytes((int)imageFileLength);
            }

            return imageData;
        }

        private static IEnumerable<string> SelectRandomEffects(int numEffects)
        {
            Random rand = new Random();

            var result = new List<string>(numEffects);
            while (result.Count() < numEffects)
            {
                var remainingEffects = EffectNames.ToArray<string>().Except(result);
                string newEffect = remainingEffects.ElementAt(rand.Next(0, remainingEffects.Count()));
                if (!result.Contains(newEffect))
                {
                    result.Add(newEffect);
                }
            }

            return result;
        }
    }

    public static class HtmlHelperExtensions
    {
        private static string GetFileContentType(string path)
        {
            if (path.EndsWith(".JPG", StringComparison.OrdinalIgnoreCase) == true)
            {
                return "image/jpeg";
            }
            else if (path.EndsWith(".GIF", StringComparison.OrdinalIgnoreCase) == true)
            {
                return "image/gif";
            }
            else if (path.EndsWith(".PNG", StringComparison.OrdinalIgnoreCase) == true)
            {
                return "image/png";
            }

            throw new ArgumentException("Unknown file type");
        }

        public static HtmlString InlineImage(this IHtmlHelper html, string path, object attributes = null)
        {
            var contentType = GetFileContentType(path);
            var env = html.ViewContext.HttpContext.ApplicationServices.GetService(typeof(IHostingEnvironment)) as IHostingEnvironment;

            using (var stream = env.WebRootFileProvider.GetFileInfo(path).CreateReadStream())
            {
                var array = new byte[stream.Length];
                stream.Read(array, 0, array.Length);

                var base64 = Convert.ToBase64String(array);

                //var props = (attributes != null) ? attributes.GetType().GetProperties().ToDictionary(x => x.Name, x => x.GetValue(attributes)) : null;

                //var attrs = (props == null)
                //    ? string.Empty
                //    : string.Join(" ", props.Select(x => string.Format("{0}=\"{1}\"", x.Key, x.Value)));

                var img = $"<img src=\"data:{contentType};base64,{base64}\" {string.Empty}/>";

                return new HtmlString(img);
            }
        }
    }
}
