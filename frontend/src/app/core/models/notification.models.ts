export interface NotificationResponse {
  id: string;
  recipientUserId: string;
  title: string;
  message: string;
  notificationType: string;
  isRead: boolean;
  createdAtUtc: string;
  readAtUtc?: string | null;
  relatedOrderId?: string | null;
  relatedAuctionId?: string | null;
}

export interface UnreadCountResponse {
  unreadCount: number;
}
