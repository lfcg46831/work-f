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
        IVatReasonsService vatReasonsService,
        IReceiptPrintPolicy receiptPrintPolicy,
        IVatService vatService) : IPdfService
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

        private sealed class TransactionBarcodeInfo
        {
            public required string BarcodeValue { get; init; }
            public required string TransactionNumber { get; init; }
            public required string TransactionOccurredAtBarcode { get; init; }
            public required string TransactionOccurredAtDisplay { get; init; }
            public required string OperatorCode { get; init; }
            public required string OperatorName { get; init; }
            public required string PosCode { get; init; }
            public required string StoreCode { get; init; }
        }

        private const string NoStructure = "Sem Categoria";
        private const string DefaultContactlessIndicatorToken = "@@logo_CTLS@@";

        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<PosStatusService> _logger = logger;
        private readonly IOperatorService _operatorService = operatorService;
        private readonly IStructureService _structureService = structureService;
        private readonly IVatReasonsService _vatReasonsService = vatReasonsService;
        private readonly IReceiptPrintPolicy _receiptPrintPolicy = receiptPrintPolicy;
        private readonly IVatService _vatService = vatService;

        #region Public methods

        public string BuildCustomReceipt(Stores store, string customReceiptBase64)
        {
            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdfDoc = new PdfDocument(writer);

            PdfFont font = PdfFontFactory.CreateFont(StandardFonts.COURIER);

            var block = TryBuildMultibancoReceiptBlock(customReceiptBase64, "Error decoding receipt.");

            if (block == null)
            {
                return string.Empty;
            }

            const float lineHeight = 8f;
            const float fontSize = 7f;
            const float firstPageHeaderY = 700f;
            const float otherPagesStartY = 755f;
            const float minY = 50f;

            var pageIndex = 1;
            var page = pdfDoc.AddNewPage();
            var canvas = new PdfCanvas(page);

            DrawLogotype(canvas);
            DrawPageNumber(canvas, font, 565, 20, pageIndex, fontSize);

            var receiptBlockWidth = CalculateFooterBlockWidth(font, block, fontSize);
            var receiptLeftX = CalculateCenteredBlockLeftX(canvas, receiptBlockWidth);

            var headerBottomY = DrawHeaderInformation(canvas, font, firstPageHeaderY, lineHeight, store);
            var separatorY = headerBottomY - (lineHeight * 2);
            DrawSeparatorLine(canvas, font, receiptLeftX, separatorY, fontSize, receiptBlockWidth);

            float currentY = separatorY - (lineHeight * 2);

            foreach (var line in block.Lines)
            {
                if (currentY < minY)
                {
                    pageIndex++;
                    page = pdfDoc.AddNewPage();
                    canvas = new PdfCanvas(page);
                    DrawLogotype(canvas);
                    DrawPageNumber(canvas, font, 565, 20, pageIndex, fontSize);
                    currentY = otherPagesStartY;
                }

                if (line.IsContactlessIcon)
                {
                    DrawCenteredContactlessIndicator(canvas, receiptLeftX, receiptBlockWidth, currentY - 2f);
                }
                else
                {
                    WriteText(canvas, font, receiptLeftX, currentY, fontSize, line.Text);
                }

                currentY -= lineHeight;
            }

            pdfDoc.Close();

            return Convert.ToBase64String(ms.ToArray());
        }

        private float DrawHeaderInformation(PdfCanvas canvas, PdfFont font, float y, float lineHeight, Stores store)
        {
            var storeName = store.Name;
            var storePhoneNumber = string.Empty;

            var storeAddress = $"{(string.IsNullOrWhiteSpace(store.Address1) ? "" : store.Address1 + ", ")}" +
                $"{(string.IsNullOrWhiteSpace(store.Address2) ? "" : store.Address2 + ", ")}" +
                $"{store.PostalCode ?? ""} {store.PostalCodeAddress ?? ""}".Trim() ?? string.Empty;

            WriteTextCentered(canvas, font, y, storeName, 8);
            y -= lineHeight;
            WriteTextCentered(canvas, font, y, $"Telef.Nr.  {storePhoneNumber}");
            y -= lineHeight;
            WriteTextCentered(canvas, font, y, storeAddress);

            return y;
        }

        public ReceiptResponse BuildReceipt(Stores store, Operators op, Basket basket, List<ReceiptResponse> merchantReceipts, bool isReturn, bool isSecondWay, bool isDuplicate)
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
            DrawHeader(canvas, font, boldFont, store, op, basket, isReturn, isSecondWay, isDuplicate, pageIndex);

            var transportValue = DrawArticles(
                pdfDoc, font, boldFont, layoutBackground, store, op, basket, isReturn, isSecondWay, isDuplicate, ref pageIndex, ref startY, lineHeight, minY, ref page, ref canvas);

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
                isSecondWay,
                isDuplicate,
                ref pageIndex,
                ref page,
                ref canvas,
                font,
                boldFont,
                notesText,
                basket.PaymentLines,
                basket.ReturnLines,
                startY,
                125f,
                transportValue);

            AddResumeVat(canvas, font, lineHeight, basket.VatLines);

            WriteTextRightAligned(canvas, font, 260, 132, 7, volumes.ToString());
            WriteTextRightAligned(canvas, font, 393, 132, 7, weigthTotal.ToString() + " Kgs.");

            if (isReturn)
            {
                WriteText(canvas, font, 190, 116, 7, "Tomei conhecimento das regularizacoes");
                WriteText(canvas, font, 190, 108, 7, "do IVA, No. 5, Art. 78 do CIVA.");
            }

            WriteText(canvas, font, 430, 114, 7, "Mercadoria");
            WriteTextRightAligned(canvas, font, 555, 114, 7, transportValue.ToFixed().ToString("F2"));

            WriteText(canvas, font, 430, 94, 7, "Imposto");
            WriteTextRightAligned(canvas, font, 555, 94, 7, vatTotal);

            WriteText(canvas, boldFont, 430, 74, 8, "Total");
            WriteTextRightAligned(canvas, boldFont, 560, 74, 8, total);

            var hasRecoveryReceipt = false;
            if (isReturn)
            {
                AddReturnInformation(store, basket.ReturnLines, merchantReceipts);
            }
            else
            {
                hasRecoveryReceipt = AddPaymentInformation(store, basket.PaymentLines, merchantReceipts);
            }

            pdfDoc.Close();

            var receipt = Convert.ToBase64String(ms.ToArray());

            var result = new ReceiptResponse()
            {
                IsMerchantReceipt = false,
                ReceiptContent = receipt,
                ReceiptContentType = "application/pdf",
                ReceiptType = hasRecoveryReceipt && !isSecondWay ? TransactionReceiptType.RecoveryReceipt : TransactionReceiptType.NormalReceipt
            };

            return result;
        }

        public string BuildSuspendedBasketReceipt(Basket? basket, bool withArticles)
        {
            if (basket == null)
                return string.Empty;

            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdfDoc = new PdfDocument(writer);

            PdfFont font = PdfFontFactory.CreateFont(StandardFonts.COURIER);
            PdfFont boldFont = PdfFontFactory.CreateFont(StandardFonts.COURIER_BOLD);
            var transactionBarcodeInfo = BuildTransactionBarcodeInfo(basket);
            var vatIdentifiersByCode = new Dictionary<int, string?>();
            var pageIndex = 1;

            var (page, canvas) = CreateTransactionReceiptPage(pdfDoc, font, transactionBarcodeInfo, pageIndex);

            const float firstPageStartY = 752f;
            const float lineHeight = 8f;
            const float minY = 50f;
            float startY = firstPageStartY;

            if (withArticles && basket.ArticleLines?.Count > 0)
            {
                var allStructure = _structureService.GetAllStructures(true);

                DrawSuspendedArticles(
                    pdfDoc,
                    font,
                    basket.ArticleLines,
                    basket.VatLines,
                    allStructure,
                    vatIdentifiersByCode,
                    transactionBarcodeInfo,
                    ref pageIndex,
                    ref page,
                    ref canvas,
                    ref startY,
                    lineHeight,
                    minY,
                    firstPageStartY);
            }

            startY -= lineHeight * 2;
            EnsureTransactionReceiptPageBreak(pdfDoc, font, transactionBarcodeInfo, ref pageIndex, ref page, ref canvas, ref startY, minY, firstPageStartY);
            WriteTextCentered(canvas, boldFont, startY, "** TRANSAÇÃO GRAVADA **", 10);
            startY -= lineHeight * 2;
            WriteTextCentered(canvas, font, startY, $"Atendido por: {transactionBarcodeInfo.OperatorName}");

            pdfDoc.Close();

            return Convert.ToBase64String(ms.ToArray());
        }

        public string BuildReceiptWithdrawal(
            Stores store,
            Operators? operatorLogged,
            long transactionNumber,
            int storeCode,
            int posCode,
            int operatorCode,
            WithdrawalReceipt withdrawalReceipt,
            EndOfShift endOfShift,
            bool withdrawalAbandoned,
            List<BoxControl>? previousBoxControl,
            List<BasketPayment>? automaticWithdrawal,
            List<Tenders> tenders)
        {
            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdfDoc = new PdfDocument(writer);

            PdfFont font = PdfFontFactory.CreateFont(StandardFonts.COURIER);
            PdfFont boldFont = PdfFontFactory.CreateFont(StandardFonts.COURIER_BOLD);

            var safePreviousBoxControl = previousBoxControl ?? [];
            var safeAutomaticWithdrawal = automaticWithdrawal ?? [];

            DrawWithdrawalDrawerReceiptPage(
                pdfDoc,
                font,
                boldFont,
                store,
                operatorLogged,
                transactionNumber,
                storeCode,
                posCode,
                operatorCode,
                withdrawalReceipt,
                endOfShift,
                withdrawalAbandoned,
                safeAutomaticWithdrawal,
                tenders,
                1);

            DrawWithdrawalEsegurReceiptPage(
                pdfDoc,
                font,
                boldFont,
                store,
                transactionNumber,
                storeCode,
                posCode,
                operatorCode,
                withdrawalReceipt,
                endOfShift,
                withdrawalAbandoned,
                safePreviousBoxControl,
                safeAutomaticWithdrawal,
                tenders,
                2);

            pdfDoc.Close();

            return Convert.ToBase64String(ms.ToArray());
        }

        private void DrawLogotype(PdfCanvas canvas)
        {
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

            const float placeholderWidth = 172.071f;
            const float placeholderHeight = 53.01599f;
            const float leftMargin = 20f;
            const float topMargin = 20f;
            var pageHeight = canvas.GetDocument().GetDefaultPageSize().GetHeight();
            var bottom = pageHeight - topMargin - placeholderHeight;

            DrawImage(canvas, source, placeholderWidth, placeholderHeight, leftMargin, bottom);
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

        private decimal DrawArticles(
            PdfDocument pdfDoc,
            PdfFont font,
            PdfFont boldFont,
            PdfFormXObject layoutBackground,
            Stores store,
            Operators op,
            Basket basket,
            bool isReturn,
            bool isSecondWay,
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
                    DrawHeader(canvas, font, boldFont, store, op, basket, isReturn, isSecondWay, isDuplicate, pageIndex);
                    DrawTransactionBarcode(pdfDoc, page, canvas, font, basket);

                    startY = 500; // reinicia posição inicial dos artigos

                    DrawBroughtForward(canvas, font, runningTotal);

                    startY -= lineHeight * 2;
                }

                var quantityPerVolume = article.ArticleType == (short)ArticleType.Weigth || article.Volume <= 0 ? 1 : (article.Quantity / article.Volume);

                // Escreve artigo
                WriteTextRightAligned(canvas, font, 75, startY, 7, article.InternalCode.Trim());
                WriteTextLimited(canvas, font, 78, startY, 7, article.LongDescription.Trim(), 30);
                WriteTextRightAligned(canvas, font, 236, startY, 7, article.Volume.ToString());
                WriteTextRightAligned(canvas, font, 264, startY, 7, quantityPerVolume.ToString());
                WriteTextRightAligned(canvas, font, 335, startY, 7, article.SaleQuantity.ToString("F3"));
                WriteTextRightAligned(canvas, font, 394, startY, 7, (article.NetBaseArticleUnitPrice ?? 0).ToString("F2"));
                WriteTextRightAligned(canvas, font, 485, startY, 7, article.BaseArticlePrice.ToString("F2"));
                WriteTextRightAligned(canvas, font, 510, startY, 7, article.ArticleVatPercentage.ToString("F1"));
                WriteTextRightAligned(canvas, font, 555, startY, 7, (article.NetArticleUnitPrice ?? 0).ToString("F2"));

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

            DrawCarriedForward(canvas, font, runningTotal);

            return runningTotal;
        }

        private static void DrawCarriedForward(PdfCanvas canvas, PdfFont font, decimal value)
        {
            WriteText(canvas, font, 430, 80, 7, "TRANSPORTE");
            WriteTextRightAligned(canvas, font, 555, 80, 7, value.ToString("0.00"));
        }

        private static void DrawBroughtForward(PdfCanvas canvas, PdfFont font, decimal value)
        {
            WriteText(canvas, font, 430, 540, 7, "TRANSPORTE");
            WriteTextRightAligned(canvas, font, 555, 540, 7, value.ToString("0.00"));
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

        private static void DrawHeader(
            PdfCanvas canvas,
            PdfFont font,
            PdfFont boldFont,
            Stores store,
            Operators op,
            Basket basket,
            bool isReturn,
            bool isSecondWay,
            bool isDuplicate,
            int pageIndex)
        {
            var hasCreditPayment = basket.PaymentLines?.Any(line => line.IsCredit) == true;
            var isSimpleInvoice = basket.CustomerFiscalInformation == null || basket.CustomerFiscalInformation.IsSimpleInvoice;

            var docName = basket.CustomerFiscalInformation.ToDocumentName(isReturn, hasCreditPayment);
            var docType = basket.CustomerFiscalInformation.ToDocumentType(isReturn, hasCreditPayment);
            var docNumber = basket.BasketSerie.Serie + "/" + basket.BasketSerie.Sequence;
            var docDate = basket.TransactionOcurredAt?.ToString("yyyy-MM-dd");
            var invoiceNumber = $"{docType} {docNumber}";

            var payerName = basket.CustomerFiscalInformation?.Name;
            var payerAddress = basket.CustomerFiscalInformation?.Address;
            var payerPostalCode = basket.CustomerFiscalInformation?.PostalCode;
            var payerCity = basket.CustomerFiscalInformation?.City;
            var payerCountry = basket.CustomerFiscalInformation?.Country;
            var nif = basket.CustomerFiscalInformation?.FiscalNumber;

            var storeName = store.Name;
            var storeAddress = store.PostalCodeAddress;
            var storePhoneNumber = string.Empty;

            var payerInternalCode = basket.CustomerFinanceInformation?.InternalCode;
            var operatorInternalCode = op.Code;

            var charge = $"{(string.IsNullOrWhiteSpace(store.Address1) ? "" : store.Address1 + ", ")}" +
                $"{(string.IsNullOrWhiteSpace(store.Address2) ? "" : store.Address2 + ", ")}" +
                $"{store.PostalCode ?? ""} {store.PostalCodeAddress ?? ""}".Trim() ?? string.Empty;
            var discharge = basket.CustomerFiscalInformation?.DischargeLocation.Replace(", ", "\r\n");
            var chargingDate = basket.TransactionOcurredAt?.ToString("yyyy-MM-dd");
            var deliveryDate = basket.TransactionOcurredAt?.ToString("yyyy-MM-dd");
            var chargingTime = basket.TransactionOcurredAt?.ToString("HH:mm");

            var atcud = $"{basket.BasketSerie.Atcud}-{basket.BasketSerie.Sequence}";

            var hashCode = HashCodeHelper.BuildHashCode(basket.BasketSerie.LastHash);
            var certificationCode = HashCodeHelper.BuildCertificationCode();

            var vats = basket.VatLines;
            var atcudBarcode = AtcudHelper.BuildAtcud(basket, isSimpleInvoice, vats);

            if (isSecondWay)
            {
                WriteTextRightAligned(canvas, boldFont, 550, 769, 7, isDuplicate ? "2ª Via Original" : "2ª Via Duplicado");
            }
            else
            {
                WriteTextRightAligned(canvas, boldFont, 550, 769, 7, isDuplicate ? "Original" : "Duplicado");
            }

            WriteText(canvas, font, 324, 749, 8, docName);
            DrawPageNumber(canvas, font, 565, 749, pageIndex, 8);
            WriteText(canvas, font, 324, 734, 8, docDate);
            WriteText(canvas, font, 420, 734, 8, invoiceNumber);

            WriteText(canvas, font, 310, 700, 8, payerName);
            WriteText(canvas, font, 310, 690, 8, payerAddress);
            WriteText(canvas, font, 310, 680, 8, payerPostalCode);
            WriteText(canvas, font, 310, 670, 8, payerCity);

            WriteText(canvas, font, 24, 665, 8, storeName);
            WriteText(canvas, font, 45, 655, 8, storeAddress);
            WriteText(canvas, font, 45, 645, 8, $"Telef.Nr.  {storePhoneNumber}");

            WriteText(canvas, font, 22, 596, 7, "Cliente : " + payerInternalCode);
            WriteText(canvas, font, 22, 585, 7, "Oper : " + operatorInternalCode);

            WriteText(canvas, font, 210, 598, 7, charge);
            WriteMultilineText(canvas, font, 210, 587, 7, discharge);
            WriteText(canvas, font, 394, 596, 7, chargingDate);
            WriteText(canvas, font, 394, 585, 7, deliveryDate);
            WriteText(canvas, font, 454, 596, 7, chargingTime);
            WriteText(canvas, font, 486, 596, 7, $"{payerCountry} {nif}");

            if (pageIndex == 1 && !string.IsNullOrWhiteSpace(atcudBarcode))
            {
                WriteText(canvas, font, 22, 520, 7, "ATCUD:" + atcud);
                WriteText(canvas, font, 22, 512, 7, hashCode + "-Processado por programa certificado " + certificationCode + "/AT");

                if (isReturn)
                {
                    WriteText(canvas, font, 22, 464, 7, $"Referente ao Documento n. {basket.ParentBasketInfo?.Invoice}");
                }

                WriteQrCode(canvas, boldFont, atcud, atcudBarcode, 460, 442, 100);
            }
        }

        private void DrawSuspendedArticles(
            PdfDocument pdfDoc,
            PdfFont font,
            IList<ArticleLine> articleLines,
            IList<VatLine>? vatLines,
            List<Structures>? allStructure,
            Dictionary<int, string?> vatIdentifiersByCode,
            TransactionBarcodeInfo transactionBarcodeInfo,
            ref int pageIndex,
            ref PdfPage page,
            ref PdfCanvas canvas,
            ref float startY,
            float lineHeight,
            float minY,
            float resetY)
        {
            var groupedArticles = articleLines.GroupBy(x => x.CategoryCode);

            foreach (var group in groupedArticles)
            {
                EnsureTransactionReceiptPageBreak(pdfDoc, font, transactionBarcodeInfo, ref pageIndex, ref page, ref canvas, ref startY, minY, resetY);

                startY -= lineHeight;
                var structureName = ResolveStructureName(group.Key, allStructure);
                WriteText(canvas, font, 30, startY, 7, structureName.ToUpper());
                startY -= lineHeight;

                foreach (var item in group)
                {
                    EnsureTransactionReceiptPageBreak(pdfDoc, font, transactionBarcodeInfo, ref pageIndex, ref page, ref canvas, ref startY, minY, resetY);

                    string vatDisplay = BuildSuspendedArticleVatDisplay(item, vatLines, vatIdentifiersByCode);
                    string descDisplay = item.ShortDescription?.ToUpper() ?? string.Empty;
                    string totalDisplay = item.TotalPrice.ToString("F2");

                    switch (item.ArticleType)
                    {
                        case (short)ArticleType.Weigth:
                        case (short)ArticleType.Unit:
                            bool showBreakdown = item.ArticleType == (short)ArticleType.Weigth || item.Quantity > 1;

                            WriteText(canvas, font, 70, startY, 7, vatDisplay);
                            WriteText(canvas, font, 110, startY, 7, descDisplay);

                            if (!showBreakdown)
                            {
                                WriteTextRightAligned(canvas, font, 470, startY, 7, totalDisplay);
                                startY -= lineHeight;
                            }
                            else
                            {
                                startY -= lineHeight;

                                string qtyFormat = item.ArticleType == (short)ArticleType.Weigth ? "#0.000" : "#0";
                                string qtyDisplay = $"{item.Quantity.ToString(qtyFormat)} X {item.UnitPrice:F2}";

                                WriteText(canvas, font, 140, startY, 7, qtyDisplay);
                                WriteTextRightAligned(canvas, font, 470, startY, 7, totalDisplay);
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
                            EnsureTransactionReceiptPageBreak(pdfDoc, font, transactionBarcodeInfo, ref pageIndex, ref page, ref canvas, ref startY, minY, resetY);
                            WriteText(canvas, font, 120, startY, 7, $"{metadata.Description}: {metadata.Value}");
                            startY -= lineHeight;
                        }
                    }

                    if (TryGetSuspendedArticleDiscount(item, out var immediateDiscount))
                    {
                        EnsureTransactionReceiptPageBreak(pdfDoc, font, transactionBarcodeInfo, ref pageIndex, ref page, ref canvas, ref startY, minY, resetY);
                        WriteText(canvas, font, 140, startY, 7, "Poupança Imediata");
                        WriteTextRightAligned(canvas, font, 470, startY, 7, $"({immediateDiscount:F2})");
                        startY -= lineHeight;
                    }
                }
            }
        }

        private string ResolveStructureName(string? categoryCode, List<Structures>? allStructure)
        {
            if (!long.TryParse(categoryCode, out var parsedCategoryCode))
                return NoStructure;

            var structure = allStructure?.Find(x => x.Code == parsedCategoryCode);
            return structure?.Name ?? NoStructure;
        }

        private string BuildSuspendedArticleVatDisplay(
            ArticleLine item,
            IList<VatLine>? vatLines,
            Dictionary<int, string?> vatIdentifiersByCode)
        {
            var matchedVatLine = vatLines?.FirstOrDefault(v =>
                v.Code == item.VatCode &&
                v.Tax == item.ArticleVatPercentage &&
                string.Equals(v.VatReasonCode, item.VatReasonCode, StringComparison.OrdinalIgnoreCase));

            var fallbackVat = item.Vats?.FirstOrDefault(v =>
                v.Code == item.VatCode &&
                v.Tax == item.ArticleVatPercentage &&
                string.Equals(v.VatReasonCode, item.VatReasonCode, StringComparison.OrdinalIgnoreCase))
                ?? item.Vats?.FirstOrDefault();

            var vatIdentifier = GetVatIdentifier(item.VatCode, matchedVatLine?.Identifier, fallbackVat?.Identifier, vatIdentifiersByCode);
            var tax = matchedVatLine?.Tax ?? fallbackVat?.Tax ?? item.ArticleVatPercentage;

            return string.IsNullOrWhiteSpace(vatIdentifier)
                ? $"{tax:00}%"
                : $"{vatIdentifier} {tax:00}%";
        }

        private string? GetVatIdentifier(
            int vatCode,
            string? vatLineIdentifier,
            string? fallbackIdentifier,
            Dictionary<int, string?> vatIdentifiersByCode)
        {
            if (!string.IsNullOrWhiteSpace(vatLineIdentifier))
                return vatLineIdentifier;

            if (!vatIdentifiersByCode.TryGetValue(vatCode, out var vatIdentifier))
            {
                vatIdentifier = _vatService.GetVat(vatCode)?.Identifier;
                vatIdentifiersByCode[vatCode] = vatIdentifier;
            }

            return string.IsNullOrWhiteSpace(vatIdentifier) ? fallbackIdentifier : vatIdentifier;
        }

        private static bool TryGetSuspendedArticleDiscount(ArticleLine item, out decimal discount)
        {
            if (item.DirectDiscount > 0)
            {
                discount = item.DirectDiscount;
                return true;
            }

            if (item.OlcasDiscount > 0)
            {
                discount = item.OlcasDiscount;
                return true;
            }

            discount = 0m;
            return false;
        }

        private (PdfPage Page, PdfCanvas Canvas) CreateTransactionReceiptPage(
            PdfDocument pdfDoc,
            PdfFont font,
            TransactionBarcodeInfo transactionBarcodeInfo,
            int pageIndex)
        {
            var page = pdfDoc.AddNewPage();
            var canvas = new PdfCanvas(page);

            DrawLogotype(canvas);
            DrawTransactionBarcode(pdfDoc, page, canvas, font, transactionBarcodeInfo);
            DrawPageNumber(canvas, font, 565, 20, pageIndex, 7);

            return (page, canvas);
        }

        private void EnsureTransactionReceiptPageBreak(
            PdfDocument pdfDoc,
            PdfFont font,
            TransactionBarcodeInfo transactionBarcodeInfo,
            ref int pageIndex,
            ref PdfPage page,
            ref PdfCanvas canvas,
            ref float startY,
            float minY,
            float resetY)
        {
            if (startY >= minY)
                return;

            pageIndex++;
            (page, canvas) = CreateTransactionReceiptPage(pdfDoc, font, transactionBarcodeInfo, pageIndex);
            startY = resetY;
        }

        private TransactionBarcodeInfo BuildTransactionBarcodeInfo(Basket basket)
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
            var operatorDefined = operatorCode > 0
                ? _operatorService.GetOperator(totalCompanyCode, totalStoreCode, operatorCode)
                : _operatorService.GetOperatorLogged();

            if (operatorDefined == null)
                throw new InvalidOperationException("Operator information is required to build the transaction barcode.");

            var transactionNumber = basket.TransactionNumber.ToString("D6");
            var transactionOccurredAtBarcode = basket.TransactionOcurredAt?.ToString("yyyyMMddHHmm") ?? string.Empty;
            var transactionOccurredAtDisplay = basket.TransactionOcurredAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
            var operatorCodeValue = operatorDefined.Code.ToString("D4");
            var posCode = totalPosCode.ToString("D4");
            var storeCode = totalStoreCode.ToString("D4");

            return new TransactionBarcodeInfo
            {
                BarcodeValue = transactionNumber + transactionOccurredAtBarcode + operatorCodeValue + posCode + storeCode,
                TransactionNumber = transactionNumber,
                TransactionOccurredAtBarcode = transactionOccurredAtBarcode,
                TransactionOccurredAtDisplay = transactionOccurredAtDisplay,
                OperatorCode = operatorCodeValue,
                OperatorName = operatorDefined.Name ?? string.Empty,
                PosCode = posCode,
                StoreCode = storeCode
            };
        }

        private void DrawTransactionBarcode(PdfDocument pdfDoc, PdfPage page, PdfCanvas canvas, PdfFont font, Basket basket)
        {
            var transactionBarcodeInfo = BuildTransactionBarcodeInfo(basket);
            DrawTransactionBarcode(pdfDoc, page, canvas, font, transactionBarcodeInfo);
        }

        private void DrawTransactionBarcode(PdfDocument pdfDoc, PdfPage page, PdfCanvas canvas, PdfFont font, TransactionBarcodeInfo transactionBarcodeInfo)
        {
            var barcode = new Barcode128(pdfDoc);
            barcode.SetCodeType(Barcode128.CODE128_UCC);
            barcode.SetCode(transactionBarcodeInfo.BarcodeValue);
            barcode.SetFont(null);

            PdfFormXObject xObject = barcode.CreateFormXObject(ColorConstants.BLACK, ColorConstants.BLACK, pdfDoc);

            var img = new Image(xObject);
            img.ScaleToFit(150, 20);

            float x = 420;
            float y = 800;

            img.SetFixedPosition(x, y);

            using var imgCanvas = new Canvas(page, page.GetPageSize());
            imgCanvas.Add(img);

            WriteText(canvas, font, 420, 790, 6, transactionBarcodeInfo.TransactionNumber);
            WriteText(canvas, font, 446, 790, 6, transactionBarcodeInfo.TransactionOccurredAtDisplay);
            WriteText(canvas, font, 507, 790, 6, transactionBarcodeInfo.OperatorCode);
            WriteText(canvas, font, 525, 790, 6, transactionBarcodeInfo.PosCode);
            WriteText(canvas, font, 547, 790, 6, transactionBarcodeInfo.StoreCode);
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

        private static void WriteTextRightAligned(PdfCanvas canvas, PdfFont font, float rightX, float y, float fontSize, string? text)
        {
            if (string.IsNullOrEmpty(text)) return;

            float textWidth = font.GetWidth(text, fontSize);

            canvas.BeginText()
                  .SetFontAndSize(font, fontSize)
                  .MoveText(rightX - textWidth, y)
                  .ShowText(text)
                  .EndText();
        }

        private static void DrawPageNumber(PdfCanvas canvas, PdfFont font, float rightX, float y, int pageIndex, float fontSize)
        {
            WriteTextRightAligned(canvas, font, rightX, y, fontSize, $"Pag. {pageIndex}");
        }

        private static void WriteQrCode(PdfCanvas canvas, PdfFont font, string atcud, string content, float x, float y, float size = 100)
        {
            if (string.IsNullOrEmpty(content)) return;

            const float padding = 2f;
            const float textOffset = 2f;
            const float fontSize = 6f;
            const float textAreaHeight = 7f;

            string text = "ATCUD: " + atcud;

            var qrCode = new BarcodeQRCode(content);
            var formXObject = qrCode.CreateFormXObject(ColorConstants.BLACK, canvas.GetDocument());

            // Fundo branco que cobre QR + texto
            canvas.SaveState();
            canvas.SetFillColor(ColorConstants.WHITE);
            canvas.Rectangle(
                x - padding,
                y - padding,
                size + (padding * 2),
                size + textAreaHeight + textOffset + (padding * 2));
            canvas.Fill();
            canvas.RestoreState();

            // Calcular largura do texto e centrar em relação ao QR
            float textWidth = font.GetWidth(text, fontSize);
            float textX = x + (size - textWidth) / 2f;
            float textY = y + size + textOffset;

            WriteText(canvas, font, textX, textY, fontSize, text);

            // QRCode
            var rect = new Rectangle(x, y, size, size);
            canvas.AddXObjectFittedIntoRectangle(formXObject, rect);
        }

        private static void WriteTextCentered(PdfCanvas canvas, PdfFont font, float y, string? text, float fontSize = 7)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

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

        private bool AddPaymentInformation(Stores store, IList<BasketPayment>? paymentLines, List<ReceiptResponse> merchantReceipts)
        {
            var customerReceipts = paymentLines?.Where(p => p.StrategyType == (int)BasketPaymentType.MultiBanco).Select(p => (p.CustomerReceipt, p.ReceiptType));
            var hasRecoveryReceipt = customerReceipts?.Any(x => x.ReceiptType == (int)TransactionReceiptType.RecoveryReceipt) == true;

            var rawMerchantReceipts = paymentLines?
                .Where(p => p.StrategyType == (int)BasketPaymentType.MultiBanco)
                .Select(p => (p.MerchantReceipt, p.ReceiptType));

            if (rawMerchantReceipts?.Count() > 0)
            {
                foreach (var mr in rawMerchantReceipts)
                {
                    if (!string.IsNullOrEmpty(mr.MerchantReceipt))
                    {
                        var result = this.BuildCustomReceipt(store, mr.MerchantReceipt);

                        if (_receiptPrintPolicy.ShouldPrintMerchantReceipt((TransactionReceiptType?)mr.ReceiptType))
                        {
                            merchantReceipts.Add(new ReceiptResponse()
                            {
                                IsMerchantReceipt = true,
                                ReceiptContent = result,
                                ReceiptContentType = "application/pdf",
                                ReceiptType = (TransactionReceiptType?)mr.ReceiptType
                            });
                        }
                    }
                }
            }

            return hasRecoveryReceipt;
        }

        private void AddReturnInformation(Stores store, IList<BasketReturn>? returnLines, List<ReceiptResponse> merchantReceipts)
        {
            var rawMerchantReceipts = returnLines?
                .Where(p => p.StrategyType == (int)BasketPaymentType.MultiBanco)
                .Select(p => (p.MerchantReceipt, p.ReceiptType));

            if (rawMerchantReceipts == null)
                return;

            foreach (var mr in rawMerchantReceipts)
            {
                if (!string.IsNullOrEmpty(mr.MerchantReceipt))
                {
                    var result = this.BuildCustomReceipt(store, mr.MerchantReceipt);

                    if (_receiptPrintPolicy.ShouldPrintMerchantReceipt((TransactionReceiptType?)mr.ReceiptType))
                    {
                        merchantReceipts.Add(new ReceiptResponse()
                        {
                            IsMerchantReceipt = true,
                            ReceiptContent = result,
                            ReceiptContentType = "application/pdf",
                            ReceiptType = (TransactionReceiptType?)mr.ReceiptType
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
            bool isSecondWay,
            bool isDuplicate,
            ref int pageIndex,
            ref PdfPage page,
            ref PdfCanvas canvas,
            PdfFont font,
            PdfFont boldFont,
            string invoiceNotes,
            IList<BasketPayment>? paymentLines,
            IList<BasketReturn>? returnLines,
            float topY,
            float bottomY,
            decimal transportValue)
        {
            const float leftColumnX = 50f;
            const float rightColumnX = 335f;
            const float lineHeight = 8f;
            const float fontSize = 7f;
            const float sectionSpacing = 8f;
            const float newPageTopY = 430f;

            var notesBlock = BuildInvoiceNotesBlock(invoiceNotes);

            var customerReceipts = isReturn
                ? returnLines?
                    .Where(p => p.StrategyType == (int)BasketPaymentType.MultiBanco)
                    .Select(p => p.CustomerReceipt)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                : paymentLines?
                    .Where(p => p.StrategyType == (int)BasketPaymentType.MultiBanco)
                    .Select(p => p.CustomerReceipt)
                    .Where(r => !string.IsNullOrWhiteSpace(r));

            var receiptBlocks = BuildMultibancoReceiptBlocks(customerReceipts);

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
                    DrawCarriedForward(canvas, font, transportValue);

                    page = pdfDoc.AddNewPage();
                    canvas = new PdfCanvas(page);
                    canvas.AddXObjectAt(layoutBackground, 0, 0);

                    pageIndex++;
                    DrawHeader(canvas, font, boldFont, store, op, basket, isReturn, isSecondWay, isDuplicate, pageIndex);
                    DrawTransactionBarcode(pdfDoc, page, canvas, font, basket);

                    currentY = newPageTopY;

                    DrawBroughtForward(canvas, font, transportValue);
                    currentY -= lineHeight * 2;
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

            const float topMargin = 10f;
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

            foreach (var line in block.Lines)
            {
                if (line.IsContactlessIcon)
                {
                    DrawContactlessIndicator(canvas, x, currentY - 2f);
                }
                else
                {
                    WriteText(canvas, font, x, currentY, fontSize, line.Text);
                }

                currentY -= lineHeight;
            }
        }

        private List<FooterBlock> BuildMultibancoReceiptBlocks(IEnumerable<string?>? customerReceipts)
        {
            var result = new List<FooterBlock>();

            if (customerReceipts == null)
                return result;

            var token = _configuration.GetSection("ContactlessIndicatorToken").Value ?? DefaultContactlessIndicatorToken;

            foreach (var receiptBase64 in customerReceipts)
            {
                var block = TryBuildMultibancoReceiptBlock(receiptBase64!, "Error decoding Multibanco receipt.", token);
                if (block != null)
                {
                    result.Add(block);
                }
            }

            return result;
        }

        private FooterBlock? TryBuildMultibancoReceiptBlock(string receiptBase64, string errorMessage, string? contactlessToken = null)
        {
            string decodedReceipt;
            try
            {
                var receiptData = Convert.FromBase64String(receiptBase64);
                decodedReceipt = Encoding.UTF8.GetString(receiptData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, errorMessage);
                return null;
            }

            var token = contactlessToken ?? _configuration.GetSection("ContactlessIndicatorToken").Value ?? DefaultContactlessIndicatorToken;

            var block = new FooterBlock();

            var lines = decodedReceipt
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

            return block;
        }

        private void DrawContactlessIndicator(PdfCanvas canvas, float x, float y)
        {
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
            DrawImage(canvas, source, 28f, 18f, x, y);
        }

        private void DrawCenteredContactlessIndicator(PdfCanvas canvas, float blockX, float blockWidth, float y)
        {
            var centeredX = blockX + ((blockWidth - 28f) / 2f);
            DrawContactlessIndicator(canvas, centeredX, y);
        }

        private static void DrawSeparatorLine(PdfCanvas canvas, PdfFont font, float x, float y, float fontSize, float blockWidth)
        {
            var dashWidth = font.GetWidth("-", fontSize);
            var dashCount = Math.Max(1, (int)MathF.Floor(blockWidth / dashWidth));
            var separator = new string('-', dashCount);
            WriteText(canvas, font, x, y, fontSize, separator);
        }

        private static float CalculateFooterBlockWidth(PdfFont font, FooterBlock block, float fontSize)
        {
            const float contactlessWidth = 28f;

            var maxLineWidth = block.Lines.Count == 0
                ? 0f
                : block.Lines.Max(line => line.IsContactlessIcon
                    ? contactlessWidth
                    : font.GetWidth(line.Text ?? string.Empty, fontSize));

            return Math.Max(maxLineWidth, contactlessWidth);
        }

        private static float CalculateCenteredBlockLeftX(PdfCanvas canvas, float blockWidth)
        {
            var pageWidth = canvas.GetDocument().GetDefaultPageSize().GetWidth();
            const float leftMargin = 8f;
            return Math.Max(leftMargin, (pageWidth - blockWidth) / 2f);
        }

        private void AddResumeVat(PdfCanvas canvas, PdfFont font, float lineHeight, IList<VatLine>? vats)
        {
            if (vats == null)
                return;

            vats = [.. vats.OrderBy(x => x.Tax)];

            var groups = vats.GroupBy(x => x.Tax);

            float startY = 120;

            foreach (var group in groups)
            {
                WriteTextRightAligned(canvas, font, 43, startY, 7, group.Key.ToString("F1"));
                WriteTextRightAligned(canvas, font, 115, startY, 7, group.Sum(x => x.NetValue).ToFixed().ToString("F2"));
                WriteTextRightAligned(canvas, font, 170, startY, 7, group.Sum(x => x.TaxValue).ToFixed().ToString("F2"));

                startY -= lineHeight;
            }

            var reasons = vats.Where(x => !string.IsNullOrEmpty(x.VatReasonCode)).Select(x =>
            {
                var vatReasons = _vatReasonsService.GetVatReasons(x.VatReasonCode!);
                var invoiceReferencesList = vatReasons?.Select(x => x.InvoiceReference).ToList();
                string invoiceReferences = invoiceReferencesList != null
                ? string.Join(", ", invoiceReferencesList)
                : string.Empty;

                return invoiceReferences;
            });

            var obs = string.Join(" | ", reasons);

            WriteText(canvas, font, 50, 37, 6, obs);
        }

        private void DrawWithdrawalDrawerReceiptPage(
            PdfDocument pdfDoc,
            PdfFont font,
            PdfFont boldFont,
            Stores store,
            Operators? operatorLogged,
            long transactionNumber,
            int storeCode,
            int posCode,
            int operatorCode,
            WithdrawalReceipt withdrawalReceipt,
            EndOfShift endOfShift,
            bool withdrawalAbandoned,
            List<BasketPayment> automaticWithdrawal,
            List<Tenders> tenders,
            int pageIndex)
        {
            var page = pdfDoc.AddNewPage(PageSize.A4);
            var canvas = new PdfCanvas(page);

            const float lineHeight = 12f;
            float currentY = 800f;
            var storeName = store?.Name?.Trim() ?? string.Empty;
            var operatorName = operatorLogged?.Name ?? string.Empty;
            var total = CalculateWithdrawalTotal(withdrawalReceipt);
            var checkDigit = BuildWithdrawalCheckDigit(withdrawalReceipt, storeCode, total, appendCheckDigit: false);

            DrawPageNumber(canvas, font, 565, 20, pageIndex, 8);

            WriteTextCentered(canvas, boldFont, currentY, storeName, 10);
            currentY -= lineHeight * 1.5f;

            if (withdrawalAbandoned)
            {
                WriteTextCentered(canvas, boldFont, currentY, "** ABANDONO **", 10);
                currentY -= lineHeight * 1.5f;
            }

            WriteText(canvas, font, 40, currentY, 9, $"{storeCode:D4} {operatorName}");
            currentY -= lineHeight;
            WriteText(canvas, font, 40, currentY, 9, "Ref sangria: Loc Data  Hora");
            currentY -= lineHeight;
            WriteText(canvas, font, 40, currentY, 9, $"{storeCode:D4} {DateTime.Now:yyyy/MM/dd} {DateTime.Now:HH:mm:ss}");
            currentY -= lineHeight;
            WriteText(canvas, font, 40, currentY, 9, $"Cód Sangria: {withdrawalReceipt.SafeBag}");
            currentY -= lineHeight;
            WriteText(canvas, font, 40, currentY, 9, $"{storeCode:D4} {DateTime.Now:yyyy/MM/dd}");
            currentY -= lineHeight;

            DrawSimpleSeparator(canvas, page, currentY);
            currentY -= lineHeight;

            WriteTextCentered(canvas, boldFont, currentY, "TOTAL DE SANGRIAS", 9);
            currentY -= lineHeight * 1.2f;

            currentY = WriteWithdrawalSummaryLines(
                canvas,
                font,
                currentY,
                tenders,
                withdrawalReceipt,
                endOfShift,
                automaticWithdrawal);

            DrawSimpleSeparator(canvas, page, currentY);
            currentY -= lineHeight;

            WriteText(canvas, font, 40, currentY, 9, $"CHECK DIGIT: {checkDigit}");
            currentY -= lineHeight * 1.5f;
            WriteText(canvas, font, 40, currentY, 9, "Operador:");
            currentY -= lineHeight * 2f;
            WriteText(canvas, font, 40, currentY, 9, "Supervisor:");
            currentY -= lineHeight * 2f;
            WriteText(
                canvas,
                font,
                40,
                currentY,
                8,
                $"{transactionNumber:D6} {DateTime.Now:yyyy-MM-dd HH:mm} {operatorCode:D4} {posCode:D4} {storeCode:D4}");
        }

        private void DrawWithdrawalEsegurReceiptPage(
            PdfDocument pdfDoc,
            PdfFont font,
            PdfFont boldFont,
            Stores store,
            long transactionNumber,
            int storeCode,
            int posCode,
            int operatorCode,
            WithdrawalReceipt withdrawalReceipt,
            EndOfShift endOfShift,
            bool withdrawalAbandoned,
            List<BoxControl> previousBoxControl,
            List<BasketPayment> automaticWithdrawal,
            List<Tenders> tenders,
            int pageIndex)
        {
            var page = pdfDoc.AddNewPage(PageSize.A4);
            var canvas = new PdfCanvas(page);

            const float lineHeight = 12f;
            float currentY = 800f;
            var storeName = store?.Name?.Trim() ?? string.Empty;
            var total = CalculateWithdrawalTotal(withdrawalReceipt);
            var barcode = BuildWithdrawalCheckDigit(withdrawalReceipt, storeCode, total, appendCheckDigit: true);
            var checkDigit = barcode[^2..];

            DrawPageNumber(canvas, font, 565, 20, pageIndex, 8);

            WriteTextCentered(canvas, boldFont, currentY, storeName, 10);
            currentY -= lineHeight * 1.5f;
            WriteTextCentered(canvas, boldFont, currentY, "Sangria Externa", 10);
            currentY -= lineHeight;
            WriteTextCentered(canvas, boldFont, currentY, "ESEGUR", 10);
            currentY -= lineHeight * 1.5f;

            if (withdrawalAbandoned)
            {
                WriteTextCentered(canvas, boldFont, currentY, "** ABANDONO **", 10);
                currentY -= lineHeight * 1.5f;
            }

            var withdrawalType = withdrawalReceipt.Type == 0 ? string.Empty : withdrawalReceipt.Type.GetEnumDescription();
            if (!string.IsNullOrWhiteSpace(withdrawalType))
            {
                WriteTextCentered(canvas, font, currentY, withdrawalType, 9);
                currentY -= lineHeight;
            }

            DrawBarcode(canvas, pdfDoc, barcode, 170, currentY - 28, 250, 32);
            currentY -= lineHeight * 4f;

            WriteTextCentered(canvas, font, currentY, $"SAFEBAG: {withdrawalReceipt.SafeBag}", 9);
            currentY -= lineHeight;
            WriteTextCentered(canvas, font, currentY, $"{storeCode:D4} {DateTime.Now:yyyy-MM-dd}", 9);
            currentY -= lineHeight;

            if (withdrawalAbandoned)
            {
                WriteTextCentered(canvas, boldFont, currentY, "Abandono da Sangria", 9);
                currentY -= lineHeight;
            }

            DrawSimpleSeparator(canvas, page, currentY);
            currentY -= lineHeight;
            WriteTextCentered(canvas, boldFont, currentY, "SANGRIAS AUTOMÁTICAS", 9);
            currentY -= lineHeight * 1.2f;

            currentY = WriteAutomaticWithdrawalLines(canvas, font, currentY, automaticWithdrawal, tenders, previousBoxControl.Any());

            DrawSimpleSeparator(canvas, page, currentY);
            currentY -= lineHeight;
            WriteTextCentered(canvas, boldFont, currentY, "SANGRIAS MANUAIS", 9);
            currentY -= lineHeight * 1.2f;

            if (withdrawalReceipt.WithrawalSequence != null && withdrawalReceipt.WithrawalSequence.Any())
            {
                currentY = WriteManualWithdrawalLines(canvas, font, currentY, withdrawalReceipt, tenders, endOfShift);
                DrawSimpleSeparator(canvas, page, currentY);
                currentY -= lineHeight;
            }

            if (previousBoxControl.Any())
            {
                WriteTextCentered(canvas, boldFont, currentY, "Histórico de Sangrias do Turno", 9);
                currentY -= lineHeight * 1.2f;
                currentY = WritePreviousWithdrawalLines(canvas, font, currentY, previousBoxControl);
                DrawSimpleSeparator(canvas, page, currentY);
                currentY -= lineHeight;
            }

            WriteTextCentered(canvas, boldFont, currentY, "TOTAL DE SANGRIAS", 9);
            currentY -= lineHeight * 1.2f;

            if (previousBoxControl.Any())
            {
                currentY = WriteAutomaticWithdrawalLines(canvas, font, currentY, automaticWithdrawal, tenders, true);
            }

            if (withdrawalReceipt.WithrawalSequence != null && withdrawalReceipt.WithrawalSequence.Any())
            {
                currentY = WriteManualWithdrawalLines(canvas, font, currentY, withdrawalReceipt, tenders, endOfShift);
                DrawSimpleSeparator(canvas, page, currentY);
                currentY -= lineHeight;
            }

            var totalToDisplay = total;
            if (endOfShift.IsDrawerFundAdded && endOfShift.EndOfShiftActive)
            {
                totalToDisplay += Convert.ToDecimal(endOfShift.DrawerFund) + automaticWithdrawal.Sum(sequence => sequence.Amount);
            }

            WriteText(canvas, boldFont, 40, currentY, 9, $"TOTAL: {totalToDisplay:F2}");
            currentY -= lineHeight;
            WriteText(canvas, font, 40, currentY, 9, $"CHECK DIGIT: {checkDigit}");
            currentY -= lineHeight * 1.5f;
            WriteText(canvas, font, 40, currentY, 9, "Operador:");
            currentY -= lineHeight * 2f;
            WriteText(canvas, font, 40, currentY, 9, "Supervisor:");
            currentY -= lineHeight * 2f;
            WriteText(
                canvas,
                font,
                40,
                currentY,
                8,
                $"{transactionNumber:D6} {DateTime.Now:yyyy-MM-dd HH:mm} {operatorCode:D4} {posCode:D4} {storeCode:D4}");
        }

        private static void DrawSimpleSeparator(PdfCanvas canvas, PdfPage page, float y)
        {
            canvas.SaveState()
                .MoveTo(40, y)
                .LineTo(page.GetPageSize().GetWidth() - 40, y)
                .Stroke()
                .RestoreState();
        }

        private static void DrawBarcode(PdfCanvas canvas, PdfDocument pdfDoc, string code, float x, float y, float width, float height)
        {
            var barcode = new Barcode128(pdfDoc);
            barcode.SetCodeType(Barcode128.CODE128);
            barcode.SetCode(code);
            barcode.SetFont(null);

            var xObject = barcode.CreateFormXObject(ColorConstants.BLACK, ColorConstants.BLACK, pdfDoc);
            var rect = new Rectangle(x, y, width, height);
            canvas.AddXObjectFittedIntoRectangle(xObject, rect);
            WriteTextCentered(canvas, PdfFontFactory.CreateFont(StandardFonts.COURIER), y - 10, code, 8);
        }

        private float WriteWithdrawalSummaryLines(
            PdfCanvas canvas,
            PdfFont font,
            float currentY,
            List<Tenders> tenders,
            WithdrawalReceipt withdrawalReceipt,
            EndOfShift endOfShift,
            List<BasketPayment> automaticWithdrawal)
        {
            const float lineHeight = 12f;
            decimal totalManual = 0;
            decimal totalAutomatic = 0;

            if (withdrawalReceipt.WithrawalSequence != null && withdrawalReceipt.WithrawalSequence.Any())
            {
                foreach (var withrawalSequence in withdrawalReceipt.WithrawalSequence)
                {
                    totalManual += decimal.Round(withrawalSequence.Totality, 2);

                    var payment = tenders.FirstOrDefault(x => x.Code == (int)withrawalSequence.Type);
                    var paymentName = payment?.Name?.ToUpper() ?? string.Empty;

                    WriteText(canvas, font, 40, currentY, 9, paymentName);
                    WriteTextRightAligned(canvas, font, 300, currentY, 9, Strings.Format(withrawalSequence.CashType, "F2"));
                    WriteTextRightAligned(canvas, font, 390, currentY, 9, Strings.Format((int)withrawalSequence.CashQuantity));
                    WriteTextRightAligned(canvas, font, 520, currentY, 9, Strings.Format(withrawalSequence.Totality, "F2"));
                    currentY -= lineHeight;
                }
            }

            if (endOfShift.EndOfShiftActive && automaticWithdrawal.Any())
            {
                foreach (var groupedWithdrawal in automaticWithdrawal.GroupBy(item => item.PaymentType))
                {
                    decimal paymentTypeTotal = 0;

                    foreach (var withdrawal in groupedWithdrawal)
                    {
                        paymentTypeTotal += (decimal)withdrawal.TotalToPay;
                    }

                    var payment = tenders.FirstOrDefault(x => x.Code == (int)groupedWithdrawal.Key);
                    var paymentName = payment?.Name?.ToUpper() ?? string.Empty;

                    WriteText(canvas, font, 40, currentY, 9, paymentName);
                    WriteTextRightAligned(canvas, font, 520, currentY, 9, Strings.Format(paymentTypeTotal, "F2"));
                    currentY -= lineHeight;
                    totalAutomatic += paymentTypeTotal;
                }
            }

            var total = totalManual + totalAutomatic;
            WriteText(canvas, font, 40, currentY, 9, $"TOTAL: {Strings.Format(total, "F2")}");

            return currentY - lineHeight;
        }

        private float WriteAutomaticWithdrawalLines(
            PdfCanvas canvas,
            PdfFont font,
            float currentY,
            List<BasketPayment> automaticWithdrawal,
            List<Tenders> tenders,
            bool includeBreakdown)
        {
            const float lineHeight = 12f;

            if (!includeBreakdown || automaticWithdrawal == null || automaticWithdrawal.Count == 0)
            {
                WriteText(canvas, font, 40, currentY, 9, "TOTAL: 0.00");
                return currentY - lineHeight;
            }

            decimal total = 0;
            var groupedWithdrawals = automaticWithdrawal
                .Where(item => item.PaymentType != (int)BasketPaymentType.Cash)
                .GroupBy(item => item.PaymentType);

            foreach (var groupedWithdrawal in groupedWithdrawals)
            {
                decimal paymentTypeTotal = 0;

                foreach (var withdrawal in groupedWithdrawal)
                {
                    paymentTypeTotal += withdrawal.TotalToPay;
                }

                var payment = tenders.FirstOrDefault(x => x.Code == (int)groupedWithdrawal.Key);
                var paymentName = payment?.Name?.ToUpper() ?? string.Empty;

                WriteText(canvas, font, 40, currentY, 9, paymentName);
                WriteTextRightAligned(canvas, font, 300, currentY, 9, Strings.Format(paymentTypeTotal, "F2"));
                WriteTextRightAligned(canvas, font, 390, currentY, 9, "1");
                WriteTextRightAligned(canvas, font, 520, currentY, 9, Strings.Format(paymentTypeTotal, "F2"));
                currentY -= lineHeight;
                total += paymentTypeTotal;
            }

            WriteText(canvas, font, 40, currentY, 9, $"TOTAL: {Strings.Format(total, "F2")}");
            return currentY - lineHeight;
        }

        private float WriteManualWithdrawalLines(
            PdfCanvas canvas,
            PdfFont font,
            float currentY,
            WithdrawalReceipt withdrawalReceipt,
            List<Tenders> tenders,
            EndOfShift endOfShift)
        {
            const float lineHeight = 12f;
            decimal total = 0;

            if (withdrawalReceipt.WithrawalSequence != null)
            {
                foreach (var withrawalSequence in withdrawalReceipt.WithrawalSequence)
                {
                    total += decimal.Round(withrawalSequence.Totality, 2);

                    var payment = tenders.FirstOrDefault(x => x.Code == (int)withrawalSequence.Type);
                    var paymentName = payment?.Name?.ToUpper() ?? string.Empty;

                    WriteText(canvas, font, 40, currentY, 9, paymentName);
                    WriteTextRightAligned(canvas, font, 300, currentY, 9, Strings.Format(withrawalSequence.CashType, "F2"));
                    WriteTextRightAligned(canvas, font, 390, currentY, 9, Strings.Format(withrawalSequence.CashQuantity, "F2"));
                    WriteTextRightAligned(canvas, font, 520, currentY, 9, Strings.Format(withrawalSequence.Totality, "F2"));
                    currentY -= lineHeight;
                }
            }

            if (endOfShift.IsDrawerFundAdded && endOfShift.EndOfShiftActive)
            {
                WriteText(canvas, font, 40, currentY, 9, $"FUNDO DE CAIXA {Strings.Format(endOfShift.DrawerFund, "F2")}");
                currentY -= lineHeight;
                total += Convert.ToDecimal(endOfShift.DrawerFund);
            }

            WriteText(canvas, font, 40, currentY, 9, $"TOTAL: {Strings.Format(total, "F2")}");
            return currentY - lineHeight;
        }

        private static float WritePreviousWithdrawalLines(
            PdfCanvas canvas,
            PdfFont font,
            float currentY,
            List<BoxControl> previousBoxControl)
        {
            const float lineHeight = 12f;

            foreach (var boxControl in previousBoxControl)
            {
                WriteText(canvas, font, 40, currentY, 9, boxControl.SafebagNumber);
                WriteTextRightAligned(canvas, font, 520, currentY, 9, Strings.Format(boxControl.SangriaAmount, "F2"));
                currentY -= lineHeight;
            }

            return currentY;
        }

        private static decimal CalculateWithdrawalTotal(WithdrawalReceipt withdrawalReceipt)
        {
            if (withdrawalReceipt.WithrawalSequence == null || !withdrawalReceipt.WithrawalSequence.Any())
                return 0;

            return withdrawalReceipt.WithrawalSequence.Sum(sequence => sequence.Totality);
        }

        private static string BuildWithdrawalCheckDigit(WithdrawalReceipt withdrawalReceipt, int storeCode, decimal total, bool appendCheckDigit)
        {
            int totalAsInt = Math.Abs((int)(total * 100));
            string formattedTotal = totalAsInt.ToString("0000000");
            var barcode = withdrawalReceipt.SafeBag + storeCode.ToString("D4") + DateTime.Now.ToString("MMdd") + formattedTotal;
            var checkDigit = ReturnTwoDigitsCheckDigit(barcode);

            return appendCheckDigit ? barcode + checkDigit : checkDigit;
        }

        private static string ReturnTwoDigitsCheckDigit(string barcode)
        {
            short sum = 0;

            for (int i = 0; i < barcode.Length; i++)
            {
                sum += (short)(int.Parse(barcode.Substring(i, 1)) * (i + 1));
            }

            return (sum % 99).ToString("D2");
        }

        #endregion
    }

    public interface IPdfService
    {
        string BuildCustomReceipt(Stores store, string customReceiptBase64);

        ReceiptResponse BuildReceipt(Stores store, Operators op, Basket basket, List<ReceiptResponse> merchantReceipts, bool isReturn, bool isSecondWay, bool isDuplicate);

        string BuildReceiptWithdrawal(
            Stores store,
            Operators? operatorLogged,
            long transactionNumber,
            int storeCode,
            int posCode,
            int operatorCode,
            WithdrawalReceipt withdrawalReceipt,
            EndOfShift endOfShift,
            bool withdrawalAbandoned,
            List<BoxControl>? previousBoxControl,
            List<BasketPayment>? automaticWithdrawal,
            List<Tenders> tenders);

        string BuildSuspendedBasketReceipt(Basket? basket, bool withArticles);
    }
}
