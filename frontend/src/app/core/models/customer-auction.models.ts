export interface CustomerAuction {
  id: string;
  cropId: string;
  cropName: string;
  cropType: string;
  variety?: string | null;
  quantity: number;
  unit: string;
  quantityKg: number;
  quantityMan: number;
  startingBidPrice: number;
  currentHighestBid: number;
  minimumBidIncrement: number;
  farmerName: string;
  farmLocation: string;
  startTimeUtc: string;
  endTimeUtc: string;
  status: 'LIVE' | 'UPCOMING' | 'ENDED' | string;
  primaryImageUrl?: string | null;
  images: string[];
  description?: string | null;
  createdAtUtc: string;
  serverTimeUtc: string;
  isFavorited?: boolean;
}

export interface CustomerAuctionFilter {
  search?: string;
  category?: string;
  status?: string;
  location?: string;
  sortBy?: string;
  minPricePerMan?: number;
  maxPricePerMan?: number;
  minQuantityKg?: number;
  maxQuantityKg?: number;
  endingSoon?: boolean;
  page?: number;
  pageSize?: number;
}

export interface PagedAuctions {
  items: CustomerAuction[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface AuctionBid {
  id: string;
  auctionId: string;
  customerProfileId: string;
  customerName: string;
  amount: number;
  requestedQuantityKg: number;
  requestedQuantityMan: number;
  bidTimeUtc: string;
  bidStatus: string;
  allocationStatus?: 'WON' | 'PARTIALLY_WON' | 'LOST' | string | null;
}

export interface AuctionAllocation {
  allocationId: string;
  auctionId: string;
  bidId: string;
  customerProfileId: string;
  customerName: string;
  requestedQuantityKg: number;
  allocatedQuantityKg: number;
  requestedQuantityMan: number;
  allocatedQuantityMan: number;
  winningBidAmountPerMan: number;
  totalPayableAmount: number;
  status: 'WON' | 'PARTIALLY_WON' | 'LOST' | string;
  finalizedAtUtc: string;
}

export interface AuctionResult {
  auctionId: string;
  cropId: string;
  cropName: string;
  cropType: string;
  quantity: number;
  unit: string;
  quantityMan: number;
  totalAuctionQuantityKg: number;
  totalAllocatedQuantityKg: number;
  remainingQuantityKg: number;
  auctionStatus: string;
  hasWinner: boolean;
  winningBidAmount?: number | null;
  winnerCustomerName?: string | null;
  winnerCustomerProfileId?: string | null;
  totalBids: number;
  allocations: AuctionAllocation[];
  startTimeUtc: string;
  endTimeUtc: string;
  finalizedAtUtc?: string | null;
  customerResultStatus?: 'WON' | 'PARTIALLY_WON' | 'LOST' | 'DID NOT BID' | 'NO WINNER' | string | null;
  serverTimeUtc: string;
}

export interface CustomerMyBid {
  bidId: string;
  auctionId: string;
  cropId: string;
  cropName: string;
  primaryImageUrl?: string | null;
  cropType: string;
  quantity: number;
  unit: string;
  quantityMan: number;
  requestedQuantityKg: number;
  requestedQuantityMan: number;
  customerBidAmount: number;
  currentHighestBid: number;
  minimumBidIncrement: number;
  allocatedQuantityKg?: number | null;
  allocatedQuantityMan?: number | null;
  auctionStatus: string;
  customerBidStatus: 'HIGHEST BID' | 'OUTBID' | 'WON' | 'PARTIALLY_WON' | 'LOST' | string;
  allocationStatus?: 'WON' | 'PARTIALLY_WON' | 'LOST' | string | null;
  bidTimeUtc: string;
  startTimeUtc: string;
  endTimeUtc: string;
  serverTimeUtc: string;
}

export interface CustomerOrder {
  orderId: string;
  orderNumber: string;
  auctionId: string;
  auctionPaymentId: string;
  auctionAllocationId: string;
  cropName: string;
  cropType: string;
  allocatedQuantityKg: number;
  allocatedQuantityMan: number;
  pricePerMan: number;
  totalAmount: number;
  status: string;
  fulfillmentMode: 'DELIVERY' | 'PICKUP' | string;
  createdAtUtc: string;
}

export interface CustomerOrderFilter {
  search?: string;
  status?: string;
  sortBy?: string;
}

export interface OrderStatusHistoryItem {
  historyId: string;
  previousStatus: string;
  newStatus: string;
  changedAtUtc: string;
  changedByUserId: string;
  note?: string | null;
}

export interface CustomerOrderListItem {
  orderId: string;
  orderNumber: string;
  auctionId: string;
  cropId: string;
  cropName: string;
  cropType: string;
  primaryImageUrl?: string | null;
  allocatedQuantityKg: number;
  allocatedQuantityMan: number;
  pricePerMan: number;
  totalAmount: number;
  farmerName: string;
  status: string;
  fulfillmentMode: 'DELIVERY' | 'PICKUP' | string;
  paymentStatus: string;
  createdAtUtc: string;
}

export interface CustomerOrderDetail {
  orderId: string;
  orderNumber: string;
  auctionId: string;
  cropId: string;
  cropName: string;
  cropType: string;
  variety?: string | null;
  primaryImageUrl?: string | null;
  requestedQuantityKg: number;
  requestedQuantityMan: number;
  allocatedQuantityKg: number;
  allocatedQuantityMan: number;
  pricePerMan: number;
  totalAmount: number;
  farmerName: string;
  farmLocation?: string | null;
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
  auctionStartTimeUtc: string;
  auctionEndDateUtc: string;
  auctionQuantityKg: number;
  auctionQuantityMan: number;
  winningBidAmount: number;
  auctionAllocationId: string;
  auctionPaymentId: string;
  transactionReference: string;
  paymentMethod: string;
  paidAtUtc?: string | null;
  timeline: OrderStatusHistoryItem[];
}

export interface UpdateOrderStatusRequest {
  newStatus: string;
  note?: string | null;
}

export interface UpdateFulfillmentDetailsRequest {
  fulfillmentMode: 'DELIVERY' | 'PICKUP' | string;
  deliveryAddress?: string | null;
  deliveryCity?: string | null;
  deliveryState?: string | null;
  deliveryPincode?: string | null;
  contactName?: string | null;
  contactPhone?: string | null;
  pickupDate?: string | null;
  expectedDeliveryDate?: string | null;
}

export interface AuctionPayment {
  paymentId: string;
  auctionId: string;
  cropId: string;
  cropName: string;
  cropType: string;
  quantity: number;
  unit: string;
  quantityMan: number;
  allocatedQuantityKg: number;
  allocatedQuantityMan: number;
  winningBidAmount: number;
  totalPayableAmount: number;
  currency: string;
  paymentMethod: string;
  paymentStatus: 'PENDING' | 'PROCESSING' | 'PAID' | 'FAILED' | string;
  transactionReference: string;
  winnerCustomerName: string;
  farmerName: string;
  createdAtUtc: string;
  paidAtUtc?: string | null;
  serverTimeUtc: string;
  order?: CustomerOrder | null;
}

export interface CustomerPaymentHistory {
  paymentId: string;
  auctionId: string;
  cropId: string;
  cropName: string;
  primaryImageUrl?: string | null;
  cropType: string;
  quantity: number;
  unit: string;
  quantityMan: number;
  allocatedQuantityKg: number;
  allocatedQuantityMan: number;
  winningBidAmount: number;
  totalPayableAmount: number;
  currency: string;
  paymentMethod: string;
  paymentStatus: string;
  transactionReference: string;
  createdAtUtc: string;
  paidAtUtc?: string | null;
}

export interface CustomerOrderTracking {
  orderId: string;
  orderNumber: string;
  auctionId: string;
  cropName: string;
  cropType: string;
  variety?: string | null;
  primaryImageUrl?: string | null;
  quantityKg: number;
  quantityMan: number;
  fulfillmentMode: 'DELIVERY' | 'PICKUP' | string;
  currentStatus: string;
  statusMessage: string;
  farmerName: string;
  farmLocation?: string | null;
  deliveryAddress?: string | null;
  deliveryCity?: string | null;
  deliveryState?: string | null;
  deliveryPincode?: string | null;
  contactName?: string | null;
  contactPhone?: string | null;
  pickupLocation?: string | null;
  pickupDate?: string | null;
  expectedDeliveryDate?: string | null;
  orderDateUtc: string;
  statusHistory: OrderStatusHistoryItem[];
}
