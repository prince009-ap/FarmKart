import { OrderStatusHistoryItem } from './customer-auction.models';

export interface FarmerOrderSummary {
  totalOrders: number;
  confirmedOrdersCount: number;
  readyForPickupCount: number;
  pickedUpCount: number;
  deliveredCount: number;
  completedCount: number;
}

export interface FarmerOrderFilter {
  search?: string;
  status?: string;
  sortBy?: string;
}

export interface FarmerOrderListItem {
  orderId: string;
  orderNumber: string;
  auctionId: string;
  cropId: string;
  cropName: string;
  cropType: string;
  primaryImageUrl?: string | null;
  customerName: string;
  allocatedQuantityKg: number;
  allocatedQuantityMan: number;
  pricePerMan: number;
  totalAmount: number;
  status: string;
  fulfillmentMode: 'DELIVERY' | 'PICKUP' | string;
  pickupDate?: string | null;
  expectedDeliveryDate?: string | null;
  paymentStatus: string;
  createdAtUtc: string;
}

export interface FarmerOrderDetail {
  orderId: string;
  orderNumber: string;
  auctionId: string;
  cropId: string;
  cropName: string;
  cropType: string;
  variety?: string | null;
  primaryImageUrl?: string | null;
  customerName: string;
  customerPhone?: string | null;
  customerCity?: string | null;
  customerState?: string | null;
  requestedQuantityKg: number;
  requestedQuantityMan: number;
  allocatedQuantityKg: number;
  allocatedQuantityMan: number;
  pricePerMan: number;
  totalAmount: number;
  auctionQuantityKg: number;
  auctionQuantityMan: number;
  winningBidAmountPerMan: number;
  auctionStartTimeUtc: string;
  auctionEndTimeUtc: string;
  status: string;
  fulfillmentMode: 'DELIVERY' | 'PICKUP' | string;
  deliveryAddress?: string | null;
  deliveryCity?: string | null;
  deliveryState?: string | null;
  deliveryPincode?: string | null;
  contactName?: string | null;
  contactPhone?: string | null;
  pickupLocation?: string | null;
  pickupDate?: string | null;
  expectedDeliveryDate?: string | null;
  paymentStatus: string;
  orderDateUtc: string;
  auctionAllocationId: string;
  auctionPaymentId: string;
  transactionReference: string;
  paymentMethod: string;
  paidAtUtc?: string | null;
  timeline: OrderStatusHistoryItem[];
}
