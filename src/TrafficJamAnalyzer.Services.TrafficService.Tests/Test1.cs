using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using TrafficJamAnalyzer.Services.TrafficService;

namespace TrafficJamAnalyzer.Services.TrafficService.Tests
{
    [TestClass]
    public sealed class TrafficCameraServiceTests
    {
        private Mock<ILogger<TrafficCameraService>> _mockLogger;
        private Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private HttpClient _httpClient;
        private TrafficCameraService _trafficCameraService;

        [TestInitialize]
        public void TestInit()
        {
            _mockLogger = new Mock<ILogger<TrafficCameraService>>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _trafficCameraService = new TrafficCameraService(_httpClient, _mockLogger.Object);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _httpClient?.Dispose();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Arrange & Act
            var service = new TrafficCameraService(_httpClient, _mockLogger.Object);

            // Assert
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void Constructor_WithNullHttpClient_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => 
                new TrafficCameraService(null!, _mockLogger.Object));
        }

        [TestMethod]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => 
                new TrafficCameraService(_httpClient, null!));
        }

        #endregion

        #region GetHtmlContentAsync Tests

        [TestMethod]
        public async Task GetHtmlContentAsync_WithValidUrl_ShouldReturnHtmlContent()
        {
            // Arrange
            var url = "https://example.com/test";
            var expectedContent = "<html><body>Test Content</body></html>";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedContent)
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _trafficCameraService.GetHtmlContentAsync(url);

            // Assert
            Assert.AreEqual(expectedContent, result);
            VerifyLoggerCalled(LogLevel.Information, "Fetching HTML content from URL: {Url}", Times.Once());
            VerifyLoggerCalled(LogLevel.Information, "Fetched HTML content. Length: {Length}", Times.Once());
        }

        [TestMethod]
        public async Task GetHtmlContentAsync_WithInvalidUrl_ShouldThrowHttpRequestException()
        {
            // Arrange
            var url = "https://invalid-url.com/test";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => 
                _trafficCameraService.GetHtmlContentAsync(url));
            
            VerifyLoggerCalled(LogLevel.Information, "Fetching HTML content from URL: {Url}", Times.Once());
        }

        [TestMethod]
        public async Task GetHtmlContentAsync_WithServerError_ShouldThrowHttpRequestException()
        {
            // Arrange
            var url = "https://server-error.com/test";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => 
                _trafficCameraService.GetHtmlContentAsync(url));
        }

        #endregion

        #region GetIframeSources Tests

        [TestMethod]
        public void GetIframeSources_WithValidHtml_ShouldExtractIframeSources()
        {
            // Arrange
            var htmlContent = @"
                <html>
                    <body>
                        <iframe src=""../camera1.php""></iframe>
                        <iframe src=""../camera2.php""></iframe>
                        <div>Other content</div>
                        <iframe src=""../camera3.php""></iframe>
                    </body>
                </html>";

            // Act
            var result = _trafficCameraService.GetIframeSources(htmlContent);

            // Assert
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("../camera1.php", result[0]);
            Assert.AreEqual("../camera2.php", result[1]);
            Assert.AreEqual("../camera3.php", result[2]);
            VerifyLoggerCalled(LogLevel.Information, "Extracting iframe sources from HTML content.", Times.Once());
            VerifyLoggerCalled(LogLevel.Information, "Extracted {Count} iframe sources.", Times.Once());
        }

        [TestMethod]
        public void GetIframeSources_WithNoIframes_ShouldReturnEmptyList()
        {
            // Arrange
            var htmlContent = @"
                <html>
                    <body>
                        <div>No iframes here</div>
                        <p>Just regular content</p>
                    </body>
                </html>";

            // Act
            var result = _trafficCameraService.GetIframeSources(htmlContent);

            // Assert
            Assert.AreEqual(0, result.Count);
            VerifyLoggerCalled(LogLevel.Information, "Extracted {Count} iframe sources.", Times.Once());
        }

        [TestMethod]
        public void GetIframeSources_WithMalformedIframes_ShouldHandleGracefully()
        {
            // Arrange
            var htmlContent = @"
                <html>
                    <body>
                        <iframe src=""../valid.php""></iframe>
                        <iframe src=""invalid-no-quotes></iframe>
                        <iframe></iframe>
                        <iframe src=""""></iframe>
                    </body>
                </html>";

            // Act
            var result = _trafficCameraService.GetIframeSources(htmlContent);

            // Assert
            Assert.IsTrue(result.Count >= 1); // Should get at least the valid one
            Assert.AreEqual("../valid.php", result[0]);
        }

        [TestMethod]
        public void GetIframeSources_WithNullHtml_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = Assert.ThrowsException<ArgumentException>(() => 
                _trafficCameraService.GetIframeSources(null!));
            
            Assert.AreEqual("HTML content cannot be null or empty. (Parameter 'htmlContent')", exception.Message);
            VerifyLoggerCalled(LogLevel.Warning, "HTML content is null or empty.", Times.Once());
        }

        [TestMethod]
        public void GetIframeSources_WithEmptyHtml_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = Assert.ThrowsException<ArgumentException>(() => 
                _trafficCameraService.GetIframeSources(""));
            
            Assert.AreEqual("HTML content cannot be null or empty. (Parameter 'htmlContent')", exception.Message);
            VerifyLoggerCalled(LogLevel.Warning, "HTML content is null or empty.", Times.Once());
        }

        [TestMethod]
        public void GetIframeSources_WithWhitespaceHtml_ShouldReturnEmptyList()
        {
            // Arrange
            var whitespaceHtml = "   ";

            // Act
            var result = _trafficCameraService.GetIframeSources(whitespaceHtml);

            // Assert
            Assert.AreEqual(0, result.Count);
            VerifyLoggerCalled(LogLevel.Information, "Extracting iframe sources from HTML content.", Times.Once());
            VerifyLoggerCalled(LogLevel.Information, "Extracted {Count} iframe sources.", Times.Once());
        }

        #endregion

        #region GetTrafficCameraImageUrlsAsync Tests

        [TestMethod]
        public async Task GetTrafficCameraImageUrlsAsync_WithValidHtml_ShouldReturnImageUrls()
        {
            // Arrange
            var htmlContent = @"<iframe src=""../camera1.php""></iframe>";
            var iframeData = @"imgsrc = ""image1.jpg""";
            var expectedImageUrl = "image1.jpg";

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(iframeData)
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _trafficCameraService.GetTrafficCameraImageUrlsAsync(htmlContent);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(expectedImageUrl, result[0]);
            VerifyLoggerCalled(LogLevel.Information, "Fetching traffic camera image URLs from iframes.", Times.Once());
            VerifyLoggerCalled(LogLevel.Information, "Total extracted image URLs: {Count}", Times.Once());
        }

        [TestMethod]
        public async Task GetTrafficCameraImageUrlsAsync_WithNullHtml_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                _trafficCameraService.GetTrafficCameraImageUrlsAsync(null!));
            
            Assert.AreEqual("HTML content cannot be null or empty. (Parameter 'htmlContent')", exception.Message);
            VerifyLoggerCalled(LogLevel.Warning, "HTML content is null or empty.", Times.Once());
        }

        [TestMethod]
        public async Task GetTrafficCameraImageUrlsAsync_WithEmptyHtml_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => 
                _trafficCameraService.GetTrafficCameraImageUrlsAsync(""));
            
            Assert.AreEqual("HTML content cannot be null or empty. (Parameter 'htmlContent')", exception.Message);
            VerifyLoggerCalled(LogLevel.Warning, "HTML content is null or empty.", Times.Once());
        }

        [TestMethod]
        public async Task GetTrafficCameraImageUrlsAsync_WithHttpException_ShouldHandleGracefully()
        {
            // Arrange
            var htmlContent = @"<iframe src=""../camera1.php""></iframe>";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await _trafficCameraService.GetTrafficCameraImageUrlsAsync(htmlContent);

            // Assert
            Assert.AreEqual(0, result.Count);
            VerifyLoggerCalled(LogLevel.Error, Times.Once());
            VerifyLoggerCalled(LogLevel.Information, "Total extracted image URLs: {Count}", Times.Once());
        }

        #endregion

        #region Helper Methods

        private void VerifyLoggerCalled(LogLevel logLevel, Times times)
        {
            _mockLogger.Verify(
                x => x.Log(
                    logLevel,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                times);
        }

        private void VerifyLoggerCalled(LogLevel logLevel, string message, Times times)
        {
            _mockLogger.Verify(
                x => x.Log(
                    logLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => CheckLogMessage(v, message)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                times);
        }

        private static bool CheckLogMessage(object state, string expectedMessage)
        {
            var actualMessage = state.ToString() ?? "";
            var expectedPart = expectedMessage.Split('{')[0].Trim();
            return actualMessage.Contains(expectedPart);
        }

        #endregion
    }
}
