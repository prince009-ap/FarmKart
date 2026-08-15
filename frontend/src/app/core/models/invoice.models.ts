export interface InvoiceResponse {
  invoiceId: string;
  invoiceNumber: string;
  invoiceDateUtc: string;
  orderId: string;
  orderNumber: string;
  orderDateUtc: string;
  paymentStatus: string;
  paymentReference: string;
  paidAtUtc: string;
  sellerName: string;
  sellerPhone?: string;
  sellerLocation?: string;
  buyerName: string;
  buyerPhone?: string;
  fulfillmentMode: string;
  deliveryOrPickupAddress?: string;
  cropName: string;
  cropType: string;
  variety: string;
  primaryImageUrl?: string;
  quantityKg: number;
  quantityMan: number;
  pricePerMan: number;
  subtotalAmount: number;
  taxAmount: number;
  totalAmount: number;
}
