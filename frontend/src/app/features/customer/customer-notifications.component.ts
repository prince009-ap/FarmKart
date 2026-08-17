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
  loading = signal(false);
  markingAll = signal(false);
  loadError = signal<string | null>(null);

  ngOnInit(): void {
    this.loadNotifications();
  }

  loadNotifications(): void {
    this.loadError.set(null);

    this.notificationService.getNotifications().subscribe({
      next: (data) => {
        this.notifications.set(data);
        const count = data.filter(n => !n.isRead).length;
        this.unreadCount.set(count);
      },
      error: (err) => {
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

  deleteNotification(notif: NotificationResponse, event: Event): void {
    event.stopPropagation();
    this.notificationService.deleteNotification(notif.id).subscribe({
      next: () => {
        this.notifications.update(list => list.filter(n => n.id !== notif.id));
        if (!notif.isRead) {
          this.unreadCount.update(c => Math.max(0, c - 1));
        }
        this.snackBar.open('Notification deleted.', 'Close', { duration: 2500 });
      },
      error: () => {
        this.snackBar.open('Failed to delete notification.', 'Close', { duration: 3000 });
      }
    });
  }

  clearAllHistory(): void {
    if (this.notifications().length === 0) return;
    if (!confirm('Are you sure you want to clear all notification history?')) return;

    this.notificationService.clearAllNotifications().subscribe({
      next: () => {
        this.notifications.set([]);
        this.unreadCount.set(0);
        this.snackBar.open('All notification history cleared.', 'Close', { duration: 3000 });
      },
      error: () => {
        this.snackBar.open('Failed to clear notification history.', 'Close', { duration: 3000 });
      }
    });
  }

  navigateToTarget(notif: NotificationResponse): void {
    this.markAsRead(notif);
    const title = notif.title.toLowerCase();

    if (notif.relatedOrderId) {
      this.router.navigate(['/customer/orders', notif.relatedOrderId]);
    } else if (notif.relatedAuctionId) {
      this.router.navigate(['/customer/auctions', notif.relatedAuctionId]);
    } else if (title.includes('machinery') || title.includes('rental') || title.includes('booking')) {
      if (title.includes('new machinery booking') || title.includes('new booking') || title.includes('received') || title.includes('incoming')) {
        this.router.navigate(['/customer/my-machinery/rentals']);
      } else {
        this.router.navigate(['/customer/my-rentals']);
      }
    } else if (title.includes('order')) {
      this.router.navigate(['/customer/orders']);
    } else if (title.includes('auction') || title.includes('bid')) {
      this.router.navigate(['/customer/bids']);
    }
  }

  getNotificationIcon(notif: NotificationResponse): string {
    const title = notif.title.toLowerCase();
    const type = notif.notificationType.toLowerCase();

    if (title.includes('machinery') || title.includes('rental') || title.includes('booking') || type.includes('machinery')) {
      return 'agriculture';
    }
    if (title.includes('order') || type.includes('order')) {
      return 'shopping_bag';
    }
    if (title.includes('auction') || type.includes('auction')) {
      return 'gavel';
    }
    if (title.includes('settled') || title.includes('payment')) {
      return 'payments';
    }
    return 'notifications';
  }
}
