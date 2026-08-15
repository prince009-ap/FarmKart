import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { NotificationService } from '../../core/services/notification.service';
import { NotificationResponse } from '../../core/models/notification.models';

@Component({
  selector: 'app-customer-notifications',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule
  ],
  templateUrl: './customer-notifications.component.html'
})
export class CustomerNotificationsComponent implements OnInit {
  private readonly notificationService = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  notifications = signal<NotificationResponse[]>([]);
  unreadCount = signal(0);
  loading = signal(true);
  markingAll = signal(false);
  loadError = signal<string | null>(null);

  ngOnInit(): void {
    this.loadNotifications();
  }

  loadNotifications(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.notificationService.getNotifications().subscribe({
      next: (data) => {
        this.notifications.set(data);
        const count = data.filter(n => !n.isRead).length;
        this.unreadCount.set(count);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.loadError.set('Failed to load notifications. Please try again.');
      }
    });
  }

  markAsRead(notif: NotificationResponse, event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    if (notif.isRead) return;

    this.notificationService.markAsRead(notif.id).subscribe({
      next: (updated) => {
        this.notifications.update(list => list.map(n => n.id === updated.id ? updated : n));
        this.unreadCount.update(c => Math.max(0, c - 1));
      },
      error: () => {
        this.snackBar.open('Failed to mark notification as read.', 'Close', { duration: 3000 });
      }
    });
  }

  markAllAsRead(): void {
    if (this.unreadCount() === 0) return;

    this.markingAll.set(true);
    this.notificationService.markAllAsRead().subscribe({
      next: () => {
        this.notifications.update(list => list.map(n => ({ ...n, isRead: true })));
        this.unreadCount.set(0);
        this.markingAll.set(false);
        this.snackBar.open('All notifications marked as read.', 'Close', { duration: 3000 });
      },
      error: () => {
        this.markingAll.set(false);
        this.snackBar.open('Failed to mark all as read.', 'Close', { duration: 3000 });
      }
    });
  }

  navigateToTarget(notif: NotificationResponse): void {
    this.markAsRead(notif);

    if (notif.relatedOrderId) {
      this.router.navigate(['/customer/orders', notif.relatedOrderId]);
    } else if (notif.relatedAuctionId) {
      this.router.navigate(['/customer/auctions', notif.relatedAuctionId]);
    } else if (notif.title.toLowerCase().includes('order')) {
      this.router.navigate(['/customer/orders']);
    } else if (notif.title.toLowerCase().includes('auction') || notif.title.toLowerCase().includes('bid')) {
      this.router.navigate(['/customer/bids']);
    }
  }

  getNotificationIcon(notif: NotificationResponse): string {
    const title = notif.title.toLowerCase();
    const type = notif.notificationType.toLowerCase();

    if (title.includes('order') || type.includes('order')) {
      return 'shopping_bag';
    }
    if (title.includes('auction') || title.includes('bid') || type.includes('auction')) {
      return 'gavel';
    }
    if (title.includes('payment') || title.includes('settled')) {
      return 'payments';
    }
    return 'notifications';
  }
}
