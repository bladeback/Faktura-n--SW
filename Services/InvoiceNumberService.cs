using System;

namespace InvoiceApp.Services
{
    public class InvoiceNumberService
    {
        // Very simple example: INV-YYYY-XXXX
        public string NextInvoiceNumber() => $"INV-{DateTime.Now:yyyy}-{DateTime.Now:MMddHHmm}";
        public string NextOrderNumber() => $"ORD-{DateTime.Now:yyyy}-{DateTime.Now:MMddHHmm}";
    }
}
