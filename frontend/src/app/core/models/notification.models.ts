export interface NotificationResponse {
  id: string;
  recipientUserId?: string;
  title: string;
  message: string;
  notificationType: string;
  isRead: boolean;
  readAtUtc?: string | null;
  priority?: string;
  actionUrl?: string | null;
  relatedEntityId?: string | null;
  relatedOrderId?: string | null;
  relatedAuctionId?: string | null;
  createdAtUtc: string;
}

export interface UnreadCountResponse {
  unreadCount: number;
}

export interface NotificationQueryRequest {
  filter?: string;
  category?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface PagedNotificationResponse {
  items: NotificationResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  unreadCount: number;
}
