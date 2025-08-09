using System;

namespace InvoiceApp.Services
{
    public class InvoiceNumberService
    {
        public string NextInvoiceNumber()
        {
            // FA-RRRRMMDDHHmm
            return $"FA-{DateTime.Now:yyyyMMddHHmm}";
        }

        public string NextOrderNumber()
        {
            // OBJ-RRRRMMDDHHmm
            return $"OBJ-{DateTime.Now:yyyyMMddHHmm}";
        }
    }
}