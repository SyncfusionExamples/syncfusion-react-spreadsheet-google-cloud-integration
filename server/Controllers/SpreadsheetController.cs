using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Syncfusion.EJ2.Spreadsheet;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SpreadsheetController : ControllerBase
    {
        // Read Google Cloud Storage settings from configuration
        public readonly string _bucketName;
        private readonly StorageClient _storageClient;

        // Constructor for SpreadsheetController
        public SpreadsheetController(IConfiguration configuration)
        {
            // Load Google Cloud service account credentials from JSON key file
            string keyFilePath = "path/to/service-account-key.json";
            var credentials = GoogleCredential.FromFile(keyFilePath);

            // Create a StorageClient instance using the credentials
            _storageClient = StorageClient.Create(credentials);

            // Fetch values from appsettings.json
            _bucketName = configuration.GetValue<string>("BucketName");
        }

        [HttpPost]
        [Route("OpenFromGoogleCloud")]
        public IActionResult OpenFromGoogleCloud([FromBody] FileOptions options)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    // Construct full file name
                    string fileName = options.FileName + options.Extension;

                    // Download file from Google Cloud Storage into memory
                    _storageClient.DownloadObject(_bucketName, fileName, stream);
                    stream.Position = 0;

                    // Prepare file for Syncfusion Excel processing
                    OpenRequest open = new OpenRequest
                    {
                        File = new FormFile(stream, 0, stream.Length, options.FileName, fileName)
                    };

                    // Convert Excel file to JSON using Syncfusion XlsIO
                    var result = Workbook.Open(open);

                    // Return JSON content
                    return Content(result, "application/json");
                }
            }
            catch (Exception ex)
            {
                // Log and return error message
                Console.WriteLine($"Error: {ex.Message}");
                return Content("Error occurred while processing the file.");
            }
        }

        // To receive file details from the client.
        public class FileOptions
        {
            public string FileName { get; set; } = string.Empty;
            public string Extension { get; set; } = string.Empty;
        }

        [HttpPost]
        [Route("SaveToGoogleCloud")]
        public async Task<IActionResult> SaveToGoogleCloud([FromForm] SaveSettings saveSettings)
        {
            try
            {
                // Convert spreadsheet JSON to Excel stream using Syncfusion
                Stream fileStream = Workbook.Save<Stream>(saveSettings);
                fileStream.Position = 0; // Reset stream position

                // Define filename for Google Cloud Storage
                string fileName = saveSettings.FileName + "." + saveSettings.SaveType.ToString().ToLower();

                // Upload Excel stream to Google Cloud Storage
                await _storageClient.UploadObjectAsync(_bucketName, fileName, null, fileStream);

                // Return success response
                return Ok("Excel file successfully saved to Google Cloud Storage.");
            }
            catch (Exception ex)
            {
                // Return error response
                return BadRequest("Error saving file to Google Cloud Storage: " + ex.Message);
            }
        }

        [HttpPost]
        [Route("Open")]
        public IActionResult Open([FromForm] IFormCollection openRequest)
        {
            OpenRequest open = new OpenRequest();
            if (openRequest.Files.Count != 0)
            {
                open.File = openRequest.Files[0];
                if (openRequest.ContainsKey("IsManualCalculationEnabled") && bool.TryParse(openRequest["IsManualCalculationEnabled"].ToString(), out bool flag))
                {
                    open.IsManualCalculationEnabled = flag;
                }
            }
            open.Password = openRequest["Password"];
            if (openRequest["SheetIndex"].Count != 0)
            {
                open.SheetIndex = int.Parse(openRequest["SheetIndex"].ToString());
            }
            open.SheetPassword = openRequest["SheetPassword"];
            return Content(Workbook.Open(open));
        }

        [HttpPost]
        [Route("Save")]
        public IActionResult Save([FromForm] SaveSettings saveSettings)
        {
            return Workbook.Save(saveSettings);
        }
    }
}

