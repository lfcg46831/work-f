using iText.Barcodes;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Xobject;
using iText.Layout;
using iText.Layout.Element;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using System.Runtime.InteropServices;
using System.Text;
using TotalCheckoutPOS.Services.POS.Api.Domain;
using TotalCheckoutPOS.Services.POS.Api.Domain.Enum;
using TotalCheckoutPOS.Services.POS.Api.Domain.Helpers;
using TotalCheckoutPOS.Services.POS.Api.Domain.Models;
using TotalCheckoutPOS.Services.POS.Api.Domain.Models.Article;
using TotalCheckoutPOS.Services.POS.Api.Domain.Models.Receipts;

namespace TotalCheckoutPOS.Services.POS.Api.Comunication.Services
{
    public class PdfService(
        IConfiguration configuration,
        ILogger<PosStatusService> logger,
        IOperatorService operatorService,
        IStructureService structureService,
        IReceiptPrintPolicy receiptPrintPolicy) : IPdfService
    {
        private sealed class FooterBlock
        {
            public List<FooterLine> Lines { get; } = new();

            public float GetHeight(float lineHeight) => Lines.Count * lineHeight;
        }

        private sealed class FooterLine
        {
            public string? Text { get; init; }
            public bool IsContactlessIcon { get; init; }
        }

        private const string NoStructure = "Sem Categoria";
        private const string DefaultContactlessIndicatorToken = "@@logo_CTLS@@";

        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<PosStatusService> _logger = logger;
        private readonly IOperatorService _operatorService = operatorService;
        private readonly IStructureService _structureService = structureService;
        private readonly IReceiptPrintPolicy _receiptPrintPolicy = receiptPrintPolicy;

        #region Public methods

        public string BuildCustomReceipt(string customReceiptBase64)
        {
            throw new NotImplementedException();
        }

        public string BuildReceipt(Stores store, Operators op, Basket basket, List<ReceiptResponse> merchantReceipts, bool isReturn, bool isDuplicate)
        {
            var templatePath = _configuration.GetValue<string>("InvoiceTemplatePath");

            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var reader = new PdfReader(templatePath);
            using var pdfDoc = new PdfDocument(reader, writer);

            PdfFont font = PdfFontFactory.CreateFont(StandardFonts.COURIER);
            PdfFont boldFont = PdfFontFactory.CreateFont(StandardFonts.COURIER_BOLD);

            // Cria um XObject com o layout da primeira página para reutilizar como fundo
            PdfPage templatePage = pdfDoc.GetFirstPage();
            PdfFormXObject layoutBackground = templatePage.CopyAsFormXObject(pdfDoc);

            int pageIndex = 1;
            float startY = 430;   // posição inicial da tabela
            float lineHeight = 8;
            float minY = 160;

            PdfPage page = templatePage;
            var canvas = new PdfCanvas(page);

            DrawTransactionBarcode(pdfDoc, page, canvas, font, basket);

            // Desenha cabeçalho na primeira página
            DrawHeader(canvas, font, boldFont, store, op, basket, isReturn, isDuplicate, pageIndex);

            DrawArticles(pdfDoc, font, boldFont, layoutBackground, store, op, basket, isReturn, isDuplicate, ref pageIndex, ref startY, lineHeight, minY, ref page, ref canvas);

            startY -= lineHeight;

            // Última página: observações, totais e assinatura
            var invoiceNotes = _configuration.GetValue<string>("InvoiceNotes");
            var notesText = !string.IsNullOrWhiteSpace(invoiceNotes) ? invoiceNotes : "Produto proveniente de Agricultura Biológica...";

            var volumes = basket.ArticleLines?.Sum(x => x.Volume);
            var weigthTotal = basket.ArticleLines?.Where(x => x.ArticleType == (short)ArticleType.Weigth).Sum(x => x.Quantity);

            var totalWithoutVat = Strings.Format((basket.BasketTotal.Total.ToFixed() - basket.BasketTotal.Vats.ToFixed()), "F2");
            var vatTotal = Strings.Format(basket.BasketTotal.Vats.ToFixed(), "F2");
            var total = Strings.Format(basket.BasketTotal.TotalToPay.ToFixed(), "F2");

            RenderFooterSections(
                pdfDoc,
                layoutBackground,
                store,
                op,
                basket,
                isReturn,
                isDuplicate,
                ref pageIndex,
                ref page,
                ref canvas,
                font,
                boldFont,
                notesText,
                basket.PaymentLines,
                startY,
                125f);

            WriteText(canvas, font, 200, 110, 7, volumes.ToString());
            WriteText(canvas, font, 352, 110, 7, weigthTotal.ToString() + " Kgs.");
            WriteText(canvas, font, 400, 100, 7, vatTotal);
            WriteText(canvas, font, 433, 64, 8, total);

            AddPaymentInformation(basket.PaymentLines, merchantReceipts);

            pdfDoc.Close();
            return Convert.ToBase64String(ms.ToArray());
        }

        public string BuildSuspendedBasketReceipt(Basket? basket, bool withArticles)
        {
            if (basket == null)
                return string.Empty;

            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdfDoc = new PdfDocument(writer);

            var page = pdfDoc.AddNewPage();
            var canvas = new PdfCanvas(page);
            PdfFont font = PdfFontFactory.CreateFont(StandardFonts.COURIER);

            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                path = _configuration.GetSection("LogotypeFilePathLinux").Value ?? "";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                path = _configuration.GetSection("LogotypeFilePathWin").Value ?? "";
            }

            var source = _configuration.GetValue<string>("Shared") + path;

            float pw = (float)172.071;
            float ph = (float)53.01599;
            float lf = (float)211.602;
            float bt = (float)727.508;

            this.DrawImage(canvas, source, pw, ph, lf, bt);

            float startY = 600;
            float lineHeight = 10;
            float minY = 50;

            if (withArticles && basket.ArticleLines.Count > 0)
            {
                var allStructure = _structureService.GetAllStructures(true);

                DrawSuspendedArticles(pdfDoc, font, basket.ArticleLines, allStructure, ref page, ref startY, lineHeight, minY);
            }

            startY -= lineHeight * 2;

            if (startY < minY)
            {
                page = pdfDoc.AddNewPage();
                canvas = new PdfCanvas(page);
                startY = 800;
            }

            WriteTextCentered(canvas, font, startY, "** TRANSAÇÃO GRAVADA **", 10);
            startY -= lineHeight * 2;

            pdfDoc.Close();

            return Convert.ToBase64String(ms.ToArray());
        }

        #endregion

        #region Private methods

        private static void CheckPageBreak(PdfDocument pdfDoc, ref PdfPage page, ref PdfCanvas canvas, ref float startY, float minY, float resetY)
        {
            if (startY < minY)
            {
                page = pdfDoc.AddNewPage();
                canvas = new PdfCanvas(page);

                startY = resetY;
            }
        }

        private static void DrawArticles(
            PdfDocument pdfDoc,
            PdfFont font,
            PdfFont boldFont,
            PdfFormXObject layoutBackground,
            Stores store,
            Operators op,
            Basket basket,
            bool isReturn,
            bool isDuplicate,
            ref int pageIndex,
            ref float startY,
            float lineHeight,
            float minY,
            ref PdfPage page,
            ref PdfCanvas canvas)
        {
            var articleLines = basket.ArticleLines;

            decimal runningTotal = 0m;

            foreach (var article in articleLines)
            {
                decimal articleTotal = Convert.ToDecimal(article.BaseArticlePrice);

                if (startY < minY)
                {
                    DrawCarriedForward(canvas, font, runningTotal);

                    // Nova página
                    page = pdfDoc.AddNewPage();
                    canvas = new PdfCanvas(page);

                    // Desenha fundo/layout
                    canvas.AddXObjectAt(layoutBackground, 0, 0);

                    pageIndex++;

                    // Desenha cabeçalho
                    DrawHeader(canvas, font, boldFont, store, op, basket, isReturn, isDuplicate, pageIndex);
                    DrawTransactionBarcode(pdfDoc, page, canvas, font, basket);

                    startY = 430; // reinicia posição inicial dos artigos

                    DrawBroughtForward(canvas, font, runningTotal);

                    startY -= lineHeight * 2;
                }

                var quantityPerVolume = article.ArticleType == (short)ArticleType.Weigth || article.Volume <= 0 ? 1 : (article.Quantity / article.Volume);

                // Escreve artigo
                WriteText(canvas, font, 29, startY, 7, article.InternalCode);
                WriteTextLimited(canvas, font, 78, startY, 7, article.LongDescription, 30);
                WriteText(canvas, font, 231, startY, 7, article.Volume.ToString());
                WriteText(canvas, font, 260, startY, 7, quantityPerVolume.ToString());
                WriteText(canvas, font, 316, startY, 7, article.SaleQuantity.ToString("F3"));
                WriteText(canvas, font, 375, startY, 7, (article.NetBaseArticleUnitPrice ?? 0).ToString("F2"));
                WriteText(canvas, font, 464, startY, 7, article.BaseArticlePrice.ToString("F2"));
                WriteText(canvas, font, 495, startY, 7, article.ArticleVatPercentage.ToString("F1"));
                WriteText(canvas, font, 540, startY, 7, (article.NetArticleUnitPrice ?? 0).ToString("F2"));

                runningTotal += articleTotal;

                startY -= lineHeight;

                if (article.Metadata?.Count > 0)
                {
                    foreach (var metadata in article.Metadata)
                    {
                        WriteText(canvas, font, 80, startY, 7, $"{metadata.Description}: {metadata.Value}");
                        startY -= lineHeight;
                    }
                }
            }
        }

        private static void DrawCarriedForward(PdfCanvas canvas, PdfFont font, decimal value)
        {
            WriteText(canvas, font, 400, 145, 7, "TRANSPORTE");
            WriteText(canvas, font, 500, 145, 7, value.ToString("0.00"));
        }

        private static void DrawBroughtForward(PdfCanvas canvas, PdfFont font, decimal value)
        {
            WriteText(canvas, font, 400, 445, 7, "TRANSPORTE");
            WriteText(canvas, font, 500, 445, 7, value.ToString("0.00"));
        }

        private void DrawImage(PdfCanvas canvas, string path, float placeholderWidth, float placeholderHeight, float left, float bottom)
        {
            ImageData imgData;
            try
            {
                if (Uri.TryCreate(path, UriKind.Absolute, out Uri? uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    using var http = new HttpClient();
                    byte[] bytes = http.GetByteArrayAsync(uri).GetAwaiter().GetResult();
                    imgData = ImageDataFactory.Create(bytes);
                }
                else
                {
                    imgData = ImageDataFactory.Create(System.IO.Path.GetFullPath(path));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load logotype image from source: {Source}. Check file permissions or network connectivity.", path);
                return;
            }

            float iw = imgData.GetWidth();
            float ih = imgData.GetHeight();

            float scale = Math.Min(placeholderWidth / iw, placeholderHeight / ih);
            float dw = iw * scale;
            float dh = ih * scale;
            float x = left + (placeholderWidth - dw) / 2f;
            float y = bottom + (placeholderHeight - dh) / 2f;

            canvas.SaveState();
            canvas.AddImageWithTransformationMatrix(imgData, dw, 0, 0, dh, x, y);
            canvas.RestoreState();
        }

        private static void DrawHeader(PdfCanvas canvas, PdfFont font, PdfFont boldFont, Stores store, Operators op, Basket basket, bool isReturn, bool isDuplicate, int pageIndex)
        {
            var hasCreditPayment = basket.PaymentLines?.Any(line => line.IsCredit) == true;
            var isSimpleInvoice = basket.CustomerFiscalInformation.IsSimpleInvoice();

            var docName = basket.CustomerFiscalInformation.ToDocumentName(isReturn, hasCreditPayment);
            var docType = basket.CustomerFiscalInformation.ToDocumentType(false, hasCreditPayment);
            var docNumber = basket.BasketSerie.Serie + "/" + basket.BasketSerie.Sequence;
            var docDate = basket.TransactionOcurredAt?.ToString("yyyy-MM-dd");
            var invoiceNumber = $"{docType} {docNumber}";

            var payerName = basket.CustomerFiscalInformation.Name;
            var payerAddress = basket.CustomerFiscalInformation.Address;
            var payerPostalCode = basket.CustomerFiscalInformation.PostalCode;
            var payerCity = basket.CustomerFiscalInformation.City;
            var payerCountry = basket.CustomerFiscalInformation.Country;
            var nif = basket.CustomerFiscalInformation.FiscalNumber;

            var storeName = store.Name;
            var storeAddress = store.PostalCodeAddress;
            var storePhoneNumber = string.Empty;

            var zoneInternalCode = string.Empty;
            var payerInternalCode = basket.CustomerFinanceInformation.InternalCode;
            var operatorInternalCode = op.Code;
            var vendorInternalCode = "88.888";

            var charge = $"{(string.IsNullOrWhiteSpace(store.Address1) ? "" : store.Address1 + ", ")}" +
                $"{(string.IsNullOrWhiteSpace(store.Address2) ? "" : store.Address2 + ", ")}" +
                $"{store.PostalCode ?? ""} {store.PostalCodeAddress ?? ""}".Trim() ?? string.Empty;
            var discharge = basket.CustomerFiscalInformation.DischargeLocation.Replace(", ", "\r\n");
            var chargingDate = basket.TransactionOcurredAt?.ToString("yyyy-MM-dd");
            var deliveryDate = basket.TransactionOcurredAt?.ToString("yyyy-MM-dd");
            var chargingTime = basket.TransactionOcurredAt?.ToString("HH:mm");

            var atcud = $"{basket.BasketSerie.Atcud}-{basket.BasketSerie.Sequence}";

            var hashCode = HashCodeHelper.BuildHashCode(basket.BasketSerie.LastHash);
            var certificationCode = HashCodeHelper.BuildCertificationCode();

            var vats = basket.VatLines;
            var atcudBarcode = AtcudHelper.BuildAtcud(basket, isSimpleInvoice, vats);

            WriteText(canvas, boldFont, 525, 769, 7, isDuplicate ? "Original" : "Duplicado");
            WriteText(canvas, font, 324, 749, 8, docName);
            WriteText(canvas, font, 535, 749, 8, "Pag. ");
            WriteText(canvas, font, 565, 749, 8, pageIndex.ToString());
            WriteText(canvas, font, 324, 735, 8, docDate);
            WriteText(canvas, font, 420, 735, 8, invoiceNumber);

            WriteText(canvas, font, 310, 700, 8, payerName);
            WriteText(canvas, font, 310, 690, 8, payerAddress);
            WriteText(canvas, font, 310, 680, 8, payerPostalCode);
            WriteText(canvas, font, 310, 670, 8, payerCity);

            WriteText(canvas, font, 24, 665, 8, storeName);
            WriteText(canvas, font, 45, 655, 8, storeAddress);
            WriteText(canvas, font, 45, 645, 8, $"Telef.Nr.  {storePhoneNumber}");

            WriteText(canvas, font, 22, 596, 7, "Zona : " + zoneInternalCode);
            WriteText(canvas, font, 70, 596, 7, "- Cliente : " + payerInternalCode);
            WriteText(canvas, font, 22, 585, 7, "Oper : " + operatorInternalCode);
            WriteText(canvas, font, 70, 585, 7, "- Vended. : " + vendorInternalCode);

            WriteText(canvas, font, 210, 598, 7, charge);
            WriteMultilineText(canvas, font, 210, 587, 7, discharge);
            WriteText(canvas, font, 394, 596, 7, chargingDate);
            WriteText(canvas, font, 394, 585, 7, deliveryDate);
            WriteText(canvas, font, 454, 596, 7, chargingTime);
            WriteText(canvas, font, 486, 596, 7, $"{payerCountry} {nif}");

            WriteText(canvas, font, 22, 520, 7, "ATCUD:" + atcud);
            WriteText(canvas, font, 22, 512, 7, hashCode + "-Processado por programa certificado " + certificationCode + "/AT");

            if (pageIndex == 1 && !string.IsNullOrWhiteSpace(atcudBarcode))
            {
                WriteQrCode(canvas, atcudBarcode, 460, 450, 100);
            }
        }

        private static void DrawSuspendedArticles(
            PdfDocument pdfDoc,
            PdfFont font,
            IList<ArticleLine> articleLines,
            List<Structures> allStructure,
            ref PdfPage page,
            ref float startY,
            float lineHeight,
            float minY)
        {
            var canvas = new PdfCanvas(page);
            var groupedArticles = articleLines.GroupBy(x => x.CategoryCode).Distinct();

            foreach (var group in groupedArticles)
            {
                CheckPageBreak(pdfDoc, ref page, ref canvas, ref startY, minY, 800);

                long categoryCode = long.Parse(group.Key);
                var structure = allStructure?.Find(x => x.Code == categoryCode);
                string structureName = structure != null ? structure.Name ?? NoStructure : NoStructure;

                startY -= lineHeight;
                WriteText(canvas, font, 30, startY, 7, structureName.ToUpper());
                startY -= lineHeight;

                foreach (var item in group)
                {
                    var vat = item.Vats?.FirstOrDefault();
                    if (vat == null) continue;

                    CheckPageBreak(pdfDoc, ref page, ref canvas, ref startY, minY, 800);

                    string vatDisplay = $"{vat.Identifier} {vat.Tax:00}%";
                    string descDisplay = item.ShortDescription?.ToUpper() ?? string.Empty;
                    string totalDisplay = item.TotalPrice.ToString("F2");

                    switch (item.ArticleType)
                    {
                        case (short)ArticleType.Weigth:
                        case (short)ArticleType.Unit:
                            bool showBreakdown = item.ArticleType == (short)ArticleType.Weigth || item.Quantity > 1;

                            WriteText(canvas, font, 30, startY, 7, vatDisplay);
                            WriteText(canvas, font, 70, startY, 7, descDisplay);

                            if (!showBreakdown)
                            {
                                WriteText(canvas, font, 450, startY, 7, totalDisplay);
                                startY -= lineHeight;
                            }
                            else
                            {
                                startY -= lineHeight;

                                string qtyFormat = item.ArticleType == (short)ArticleType.Weigth ? "#0.000" : "#0";
                                string qtyDisplay = $"{item.Quantity.ToString(qtyFormat)} X {item.UnitPrice:F2}";

                                WriteText(canvas, font, 100, startY, 7, qtyDisplay);
                                WriteText(canvas, font, 450, startY, 7, totalDisplay);
                                startY -= lineHeight;
                            }
                            break;

                        case (short)ArticleType.Serial:
                            if (!string.IsNullOrEmpty(item.SerialNumber))
                            {
                                WriteText(canvas, font, 70, startY, 7, $"Número Série: {item.SerialNumber}");
                                startY -= lineHeight;
                            }
                            break;
                    }

                    if (item.Metadata?.Count > 0)
                    {
                        foreach (var metadata in item.Metadata)
                        {
                            CheckPageBreak(pdfDoc, ref page, ref canvas, ref startY, minY, 800);
                            WriteText(canvas, font, 80, startY, 7, $"{metadata.Description}: {metadata.Value}");
                            startY -= lineHeight;
                        }
                    }

                    if (item.DirectDiscount > 0)
                    {
                        CheckPageBreak(pdfDoc, ref page, ref canvas, ref startY, minY, 800);
                        WriteText(canvas, font, 100, startY, 7, "Poupança Imediata");
                        WriteText(canvas, font, 450, startY, 7, $"({item.DirectDiscount:F2})");
                        startY -= lineHeight;
                    }
                }
            }
        }

        private void DrawTransactionBarcode(PdfDocument pdfDoc, PdfPage page, PdfCanvas canvas, PdfFont font, Basket basket)
        {
            var totalCompanyCode = _configuration.GetValue<string>("TotalCompanyCode");
            if (string.IsNullOrWhiteSpace(totalCompanyCode))
            {
                _logger.LogError("Configuration 'TotalCompanyCode' is not defined.");
                throw new InvalidOperationException("The 'TotalCompanyCode' configuration is required.");
            }

            var totalStoreCode = _configuration.GetValue<int>("TotalStoreCode");
            var totalPosCode = _configuration.GetValue<int>("TotalPosCode");

            int operatorCode = (int)(basket.PaymentLines?.FirstOrDefault()?.OperatorCode ?? 0);

            var operatorDefined = operatorCode > 0 ? _operatorService.GetOperator(totalCompanyCode, totalStoreCode, operatorCode) : _operatorService.GetOperatorLogged();

            var transactionNumber = basket.TransactionNumber.ToString("D6");
            var transactionOcurredAt = basket.TransactionOcurredAt?.ToString("yyyyMMddHHmm");
            var operatorCodeValue = operatorDefined.Code.ToString("D4");
            var posCode = totalPosCode.ToString("D4");
            var storeCode = totalStoreCode.ToString("D4");

            var barcodeValue = transactionNumber + transactionOcurredAt + operatorCodeValue + posCode + storeCode;

            var barcode = new Barcode128(pdfDoc);
            barcode.SetCodeType(Barcode128.CODE128_UCC);
            barcode.SetCode(barcodeValue);
            barcode.SetFont(null);

            PdfFormXObject xObject = barcode.CreateFormXObject(ColorConstants.BLACK, ColorConstants.BLACK, pdfDoc);

            var img = new Image(xObject);
            img.ScaleToFit(150, 20);

            float x = 420;
            float y = 800;

            img.SetFixedPosition(x, y);

            using var imgCanvas = new Canvas(page, page.GetPageSize());
            imgCanvas.Add(img);

            WriteText(canvas, font, 422, 790, 6, transactionNumber);
            WriteText(canvas, font, 449, 790, 6, transactionOcurredAt);
            WriteText(canvas, font, 499, 790, 6, operatorCodeValue);
            WriteText(canvas, font, 519, 790, 6, posCode);
            WriteText(canvas, font, 541, 790, 6, storeCode);
        }

        private static void WriteText(PdfCanvas canvas, PdfFont font, float x, float y, float fontSize, string? text)
        {
            if (string.IsNullOrEmpty(text)) return;

            canvas.BeginText()
                  .SetFontAndSize(font, fontSize)
                  .MoveText(x, y)
                  .ShowText(text)
                  .EndText();
        }

        private static void WriteTextLimited(PdfCanvas canvas, PdfFont font, float x, float y, float fontSize, string? text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return;
            string output = text.Length > maxChars ? text.Substring(0, maxChars) : text;
            canvas.BeginText()
                  .SetFontAndSize(font, fontSize)
                  .MoveText(x, y)
                  .ShowText(output)
                  .EndText();
        }

        private static void WriteQrCode(PdfCanvas canvas, string content, float x, float y, float size = 100)
        {
            if (string.IsNullOrEmpty(content)) return;

            const float padding = 3f;

            var qrCode = new BarcodeQRCode(content);
            var formXObject = qrCode.CreateFormXObject(ColorConstants.BLACK, canvas.GetDocument());

            // background branco com margem
            canvas.SaveState();
            canvas.SetFillColor(ColorConstants.WHITE);
            canvas.Rectangle(x - padding, y - padding, size + (padding * 2), size + (padding * 2));
            canvas.Fill();
            canvas.RestoreState();

            // define a área do QRCode no canvas
            var rect = new Rectangle(x, y, size, size);
            canvas.AddXObjectFittedIntoRectangle(formXObject, rect);
        }

        private static void WriteTextCentered(PdfCanvas canvas, PdfFont font, float y, string text, float fontSize = 7)
        {
            if (string.IsNullOrEmpty(text)) return;

            float pageWidth = canvas.GetDocument().GetDefaultPageSize().GetWidth();

            float textWidth = font.GetWidth(text, fontSize);

            float x = (pageWidth - textWidth) / 2f;

            canvas.BeginText()
                  .SetFontAndSize(font, fontSize)
                  .MoveText(x, y)
                  .ShowText(text)
                  .EndText();
        }

        private static void WriteMultilineText(PdfCanvas canvas, PdfFont font, float x, float y, float fontSize, string? text, float lineHeight = 8)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var lines = text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Split('\n');

            float currentY = y;

            foreach (var line in lines)
            {
                canvas.BeginText()
                      .SetFontAndSize(font, fontSize)
                      .MoveText(x, currentY)
                      .ShowText(line)
                      .EndText();

                currentY -= lineHeight;
            }
        }

        private void AddPaymentInformation(IList<BasketPayment>? paymentLines, List<ReceiptResponse> merchantReceipts)
        {
            var rawMerchantReceipts = paymentLines?
                .Where(p => p.StrategyType == (int)BasketPaymentType.MultiBanco)
                .Select(p => (p.MerchantReceipt, p.ReceiptType));

            if (rawMerchantReceipts == null)
                return;

            foreach (var mr in rawMerchantReceipts)
            {
                if (!string.IsNullOrEmpty(mr.MerchantReceipt))
                {
                    var result = this.BuildCustomReceipt(mr.MerchantReceipt);

                    if (_receiptPrintPolicy.ShouldPrintMerchantReceipt((ReceiptType?)mr.ReceiptType))
                    {
                        merchantReceipts.Add(new ReceiptResponse()
                        {
                            IsMerchantReceipt = true,
                            ReceiptContent = result,
                            ReceiptContentType = "application/pdf",
                            ReceiptType = (ReceiptType?)mr.ReceiptType
                        });
                    }
                }
            }
        }

        private void RenderFooterSections(
            PdfDocument pdfDoc,
            PdfFormXObject layoutBackground,
            Stores store,
            Operators op,
            Basket basket,
            bool isReturn,
            bool isDuplicate,
            ref int pageIndex,
            ref PdfPage page,
            ref PdfCanvas canvas,
            PdfFont font,
            PdfFont boldFont,
            string invoiceNotes,
            IList<BasketPayment>? paymentLines,
            float topY,
            float bottomY)
        {
            const float leftColumnX = 50f;
            const float rightColumnX = 335f;
            const float lineHeight = 8f;
            const float fontSize = 7f;
            const float sectionSpacing = 8f;
            const float newPageTopY = 430f;

            var notesBlock = BuildInvoiceNotesBlock(invoiceNotes);
            var receiptBlocks = BuildMultibancoReceiptBlocks(paymentLines);

            var sections = new List<(FooterBlock? Left, FooterBlock? Right)>();

            if (receiptBlocks.Count > 0)
            {
                sections.Add((notesBlock, receiptBlocks[0]));

                for (int i = 1; i < receiptBlocks.Count; i += 2)
                {
                    var left = receiptBlocks[i];
                    var right = i + 1 < receiptBlocks.Count ? receiptBlocks[i + 1] : null;
                    sections.Add((left, right));
                }
            }
            else
            {
                sections.Add((notesBlock, null));
            }

            float currentY = topY;

            foreach (var section in sections)
            {
                float leftHeight = section.Left?.GetHeight(lineHeight) ?? 0f;
                float rightHeight = section.Right?.GetHeight(lineHeight) ?? 0f;
                float sectionHeight = Math.Max(leftHeight, rightHeight);

                if (currentY - sectionHeight < bottomY)
                {
                    page = pdfDoc.AddNewPage();
                    canvas = new PdfCanvas(page);
                    canvas.AddXObjectAt(layoutBackground, 0, 0);

                    pageIndex++;
                    DrawHeader(canvas, font, boldFont, store, op, basket, isReturn, isDuplicate, pageIndex);
                    DrawTransactionBarcode(pdfDoc, page, canvas, font, basket);

                    currentY = newPageTopY;
                }

                if (sectionHeight > 0f)
                {
                    DrawFooterSectionBackground(canvas, page, currentY, sectionHeight);
                }

                if (section.Left != null)
                {
                    WriteFooterBlock(canvas, font, leftColumnX, currentY, fontSize, lineHeight, section.Left);
                }

                if (section.Right != null)
                {
                    WriteFooterBlock(canvas, font, rightColumnX, currentY, fontSize, lineHeight, section.Right);
                }

                currentY -= sectionHeight + sectionSpacing;
            }
        }

        private static void DrawFooterSectionBackground(PdfCanvas canvas, PdfPage page, float topY, float sectionHeight)
        {
            if (sectionHeight <= 0f)
                return;

            const float topMargin = 2f;
            const float bottomMargin = 3f;

            var pageWidth = page.GetPageSize().GetWidth();
            var rectangleTop = topY + topMargin;
            var rectangleHeight = sectionHeight + topMargin + bottomMargin;
            var rectangleBottom = rectangleTop - rectangleHeight;

            canvas.SaveState()
                  .SetFillColor(ColorConstants.WHITE)
                  .Rectangle(0f, rectangleBottom, pageWidth, rectangleHeight)
                  .Fill()
                  .RestoreState();
        }

        private static FooterBlock BuildInvoiceNotesBlock(string? text)
        {
            var block = new FooterBlock();

            if (string.IsNullOrWhiteSpace(text))
                return block;

            var lines = text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Split('\n', StringSplitOptions.None);

            foreach (var line in lines)
            {
                block.Lines.Add(new FooterLine
                {
                    Text = line
                });
            }

            return block;
        }

        private void WriteFooterBlock(
            PdfCanvas canvas,
            PdfFont font,
            float x,
            float startY,
            float fontSize,
            float lineHeight,
            FooterBlock block)
        {
            float currentY = startY;

            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                path = _configuration.GetSection("ContactlessIndicatorFilePathLinux").Value ?? "";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                path = _configuration.GetSection("ContactlessIndicatorFilePathWin").Value ?? "";
            }

            var source = (_configuration.GetValue<string>("Shared") ?? "") + path;

            foreach (var line in block.Lines)
            {
                if (line.IsContactlessIcon)
                {
                    DrawImage(canvas, source, 18f, 8f, x, currentY - 2f);
                }
                else
                {
                    WriteText(canvas, font, x, currentY, fontSize, line.Text);
                }

                currentY -= lineHeight;
            }
        }

        private List<FooterBlock> BuildMultibancoReceiptBlocks(IList<BasketPayment>? paymentLines)
        {
            var result = new List<FooterBlock>();

            var customerReceipts = paymentLines?
                .Where(p => p.StrategyType == (int)BasketPaymentType.MultiBanco)
                .Select(p => p.CustomerReceipt)
                .Where(r => !string.IsNullOrWhiteSpace(r));

            if (customerReceipts == null)
                return result;

            var token = _configuration.GetSection("ContactlessIndicatorToken").Value ?? DefaultContactlessIndicatorToken;

            foreach (var receiptBase64 in customerReceipts)
            {
                string decodedCustomerReceipt;
                try
                {
                    var customerReceiptData = Convert.FromBase64String(receiptBase64!);
                    decodedCustomerReceipt = Encoding.UTF8.GetString(customerReceiptData);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao descodificar talão Multibanco.");
                    continue;
                }

                var block = new FooterBlock();

                var lines = decodedCustomerReceipt
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Split('\n', StringSplitOptions.None);

                foreach (var line in lines)
                {
                    var normalizedLine = line
                        .Replace("\u001B", string.Empty)
                        .TrimEnd();

                    if (normalizedLine.Trim() == token)
                    {
                        block.Lines.Add(new FooterLine
                        {
                            IsContactlessIcon = true
                        });
                    }
                    else
                    {
                        block.Lines.Add(new FooterLine
                        {
                            Text = normalizedLine
                        });
                    }
                }

                result.Add(block);
            }

            return result;
        }

        #endregion
    }

    public interface IPdfService
    {
        string BuildCustomReceipt(string customReceiptBase64);

        string BuildReceipt(Stores store, Operators op, Basket basket, List<ReceiptResponse> merchantReceipts, bool isReturn, bool isDuplicate);

        string BuildSuspendedBasketReceipt(Basket? basket, bool withArticles);
    }
}
