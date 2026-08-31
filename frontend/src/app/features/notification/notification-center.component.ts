import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NotificationService } from '../../core/services/notification.service';
import { NotificationResponse, PagedNotificationResponse } from '../../core/models/notification.models';

@Component({
  selector: 'app-notification-center',
  standalone: true,
  imports: [
    TranslatePipe,CommonModule, FormsModule, RouterModule],
  template: `
    <div class="min-h-screen bg-slate-50 py-8 px-4 sm:px-6 lg:px-8">
      <div class="max-w-5xl mx-auto space-y-6">
        <!-- Header -->
        <div class="bg-white rounded-2xl shadow-sm border border-slate-200/80 p-6 flex flex-col md:flex-row items-start md:items-center justify-between gap-4">
          <div>
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-xl bg-emerald-100 text-emerald-700 flex items-center justify-center font-bold">
                🔔
              </div>
              <div>
                <h1 class="text-2xl font-bold text-slate-900">Notification Center</h1>
                <p class="text-xs text-slate-500">Manage all your updates, alerts, auction statuses, orders, and disputes.</p>
              </div>
            </div>
          </div>
          <div class="flex items-center gap-3">
            <button (click)="markAllAsRead()" [disabled]="unreadCount === 0" class="px-4 py-2 text-xs font-semibold text-emerald-700 bg-emerald-50 hover:bg-emerald-100 disabled:opacity-50 rounded-xl transition">
              Mark All as Read
            </button>
            <button (click)="clearAll()" [disabled]="totalCount === 0" class="px-4 py-2 text-xs font-semibold text-red-600 bg-red-50 hover:bg-red-100 disabled:opacity-50 rounded-xl transition">
              Clear All
            </button>
          </div>
        </div>

        <!-- Filters Bar -->
        <div class="bg-white rounded-2xl shadow-sm border border-slate-200/80 p-4 space-y-4">
          <div class="flex flex-wrap items-center justify-between gap-4">
            <!-- Status Tabs -->
            <div class="flex bg-slate-100 p-1 rounded-xl">
              <button (click)="setFilter('all')" [class.bg-white]="filter === 'all'" [class.shadow-sm]="filter === 'all'" class="px-4 py-1.5 text-xs font-semibold rounded-lg transition text-slate-700">
                All ({{ totalCount }})
              </button>
              <button (click)="setFilter('unread')" [class.bg-white]="filter === 'unread'" [class.shadow-sm]="filter === 'unread'" class="px-4 py-1.5 text-xs font-semibold rounded-lg transition text-slate-700 flex items-center gap-1.5">
                Unread
                <span *ngIf="unreadCount > 0" class="bg-emerald-600 text-white text-[10px] px-1.5 py-0.5 rounded-full">{{ unreadCount }}</span>
              </button>
              <button (click)="setFilter('read')" [class.bg-white]="filter === 'read'" [class.shadow-sm]="filter === 'read'" class="px-4 py-1.5 text-xs font-semibold rounded-lg transition text-slate-700">
                Read
              </button>
            </div>

            <!-- Category & Search -->
            <div class="flex flex-wrap items-center gap-3 w-full sm:w-auto">
              <select [(ngModel)]="selectedCategory" (change)="onCategoryChange()" class="text-xs rounded-xl border-slate-300 focus:border-emerald-500 focus:ring-emerald-500 p-2 bg-slate-50">
                <option value="">{{ 'marketplace.allCategories' | translate }}</option>
                <option value="auction">Auctions</option>
                <option value="order">Orders</option>
                <option value="payment">{{ 'nav.payments' | translate }}</option>
                <option value="rental">Machinery Rentals</option>
                <option value="review">Reviews</option>
                <option value="dispute">Disputes & Reports</option>
                <option value="system">System Alerts</option>
              </select>

              <div class="relative flex-1 sm:w-64">
                <input type="text" [(ngModel)]="searchTerm" (keyup.enter)="onSearch()" placeholder="Search notifications..." class="w-full text-xs rounded-xl border-slate-300 focus:border-emerald-500 focus:ring-emerald-500 pl-8 pr-3 py-2 bg-slate-50" />
                <svg class="w-4 h-4 text-slate-400 absolute left-2.5 top-2.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                </svg>
              </div>
            </div>
          </div>
        </div>

        <!-- Notification List -->
        <div class="bg-white rounded-2xl shadow-sm border border-slate-200/80 overflow-hidden divide-y divide-slate-100">
          <div *ngIf="notifications.length === 0" class="p-12 text-center">
            <div class="w-12 h-12 bg-slate-100 rounded-full flex items-center justify-center mx-auto mb-3 text-slate-400 text-xl">🔕</div>
            <h3 class="text-base font-semibold text-slate-800">No Notifications</h3>
            <p class="text-xs text-slate-500 mt-1">No updates matched your selected filters or search terms.</p>
          </div>

          <div *ngFor="let n of notifications" [class.bg-emerald-50\/40]="!n.isRead" class="p-4 hover:bg-slate-50/80 transition flex items-start justify-between gap-4 group">
            <div (click)="onNotificationClick(n)" class="flex items-start gap-3 cursor-pointer flex-1">
              <!-- Type Icon -->
              <div [ngClass]="getCategoryBadgeClass(n.notificationType)" class="w-9 h-9 rounded-xl flex items-center justify-center text-sm font-bold shrink-0 mt-0.5 shadow-xs">
                {{ getCategoryIcon(n.notificationType) }}
              </div>

              <div class="space-y-1">
                <div class="flex items-center gap-2">
                  <h4 class="text-sm font-semibold text-slate-900" [class.font-bold]="!n.isRead">{{ n.title }}</h4>
                  <span *ngIf="!n.isRead" class="w-2 h-2 rounded-full bg-emerald-600"></span>
                  <span *ngIf="n.priority === 'High'" class="px-2 py-0.5 text-[10px] font-bold text-red-700 bg-red-100 rounded-full">HIGH</span>
                </div>
                <p class="text-xs text-slate-600 leading-relaxed">{{ n.message }}</p>
                <div class="text-[11px] text-slate-400 font-medium">
                  {{ n.createdAtUtc | date:'medium' }}
                </div>
              </div>
            </div>

            <!-- Actions -->
            <div class="flex items-center gap-2 opacity-80 group-hover:opacity-100 transition">
              <button *ngIf="!n.isRead" (click)="markAsRead(n.id, $event)" title="Mark as read" class="p-1.5 text-slate-400 hover:text-emerald-600 rounded-lg hover:bg-emerald-50 transition">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                </svg>
              </button>
              <button (click)="deleteNotification(n.id, $event)" title="Delete" class="p-1.5 text-slate-400 hover:text-red-600 rounded-lg hover:bg-red-50 transition">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                </svg>
              </button>
            </div>
          </div>
        </div>

        <!-- Pagination Footer -->
        <div *ngIf="totalPages > 1" class="flex items-center justify-between bg-white rounded-2xl p-4 border border-slate-200/80">
          <p class="text-xs text-slate-500">Page {{ currentPage }} of {{ totalPages }}</p>
          <div class="flex gap-2">
            <button (click)="changePage(currentPage - 1)" [disabled]="currentPage === 1" class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-100 hover:bg-slate-200 disabled:opacity-40 transition">
              Previous
            </button>
            <button (click)="changePage(currentPage + 1)" [disabled]="currentPage === totalPages" class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-100 hover:bg-slate-200 disabled:opacity-40 transition">
              Next
            </button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class NotificationCenterComponent implements OnInit {
  private readonly notificationService = inject(NotificationService);
  private readonly router = inject(Router);

  notifications: NotificationResponse[] = [];
  filter = 'all';
  selectedCategory = '';
  searchTerm = '';
  currentPage = 1;
  pageSize = 15;

  totalCount = 0;
  unreadCount = 0;
  totalPages = 1;
  isLoading = false;

  ngOnInit(): void {
    this.loadNotifications();
  }

  loadNotifications(): void {
    this.isLoading = true;
    this.notificationService.getPagedNotifications({
      filter: this.filter,
      category: this.selectedCategory,
      search: this.searchTerm,
      page: this.currentPage,
      pageSize: this.pageSize
    }).subscribe({
      next: (res: PagedNotificationResponse) => {
        this.notifications = res.items;
        this.totalCount = res.totalCount;
        this.unreadCount = res.unreadCount;
        this.totalPages = res.totalPages;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  setFilter(filter: string): void {
    this.filter = filter;
    this.currentPage = 1;
    this.loadNotifications();
  }

  onCategoryChange(): void {
    this.currentPage = 1;
    this.loadNotifications();
  }

  onSearch(): void {
    this.currentPage = 1;
    this.loadNotifications();
  }

  changePage(page: number): void {
    if (page >= 1 && page <= this.totalPages) {
      this.currentPage = page;
      this.loadNotifications();
    }
  }

  markAsRead(id: string, event: Event): void {
    event.stopPropagation();
    this.notificationService.markAsRead(id).subscribe(() => this.loadNotifications());
  }

  markAllAsRead(): void {
    this.notificationService.markAllAsRead().subscribe(() => this.loadNotifications());
  }

  deleteNotification(id: string, event: Event): void {
    event.stopPropagation();
    this.notificationService.deleteNotification(id).subscribe(() => this.loadNotifications());
  }

  clearAll(): void {
    if (confirm('Are you sure you want to clear all notifications?')) {
      this.notificationService.clearAllNotifications().subscribe(() => this.loadNotifications());
    }
  }

  onNotificationClick(n: NotificationResponse): void {
    if (!n.isRead) {
      this.notificationService.markAsRead(n.id).subscribe();
    }

    if (n.actionUrl) {
      this.router.navigateByUrl(n.actionUrl);
    } else if (n.relatedAuctionId) {
      this.router.navigate(['/customer/auctions', n.relatedAuctionId]);
    } else if (n.relatedOrderId) {
      this.router.navigate(['/customer/orders', n.relatedOrderId]);
    }
  }

  getCategoryIcon(type: string): string {
    const t = (type || '').toLowerCase();
    if (t.includes('auction')) return '🔨';
    if (t.includes('order')) return '📦';
    if (t.includes('payment') || t.includes('settlement')) return '💳';
    if (t.includes('rental') || t.includes('driver')) return '🚜';
    if (t.includes('review')) return '⭐';
    if (t.includes('dispute') || t.includes('report')) return '⚠️';
    return '🔔';
  }

  getCategoryBadgeClass(type: string): string {
    const t = (type || '').toLowerCase();
    if (t.includes('auction')) return 'bg-amber-100 text-amber-800';
    if (t.includes('order')) return 'bg-blue-100 text-blue-800';
    if (t.includes('payment') || t.includes('settlement')) return 'bg-emerald-100 text-emerald-800';
    if (t.includes('rental') || t.includes('driver')) return 'bg-indigo-100 text-indigo-800';
    if (t.includes('dispute') || t.includes('report')) return 'bg-red-100 text-red-800';
    return 'bg-slate-100 text-slate-800';
  }
}
