import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CustomerNotificationsComponent } from './customer-notifications.component';
import { NotificationService } from '../../core/services/notification.service';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { NotificationResponse } from '../../core/models/notification.models';
import { vi, describe, beforeEach, it, expect } from 'vitest';

describe('CustomerNotificationsComponent', () => {
  let component: CustomerNotificationsComponent;
  let fixture: ComponentFixture<CustomerNotificationsComponent>;
  let mockNotificationService: {
    getNotifications: ReturnType<typeof vi.fn>;
    markAsRead: ReturnType<typeof vi.fn>;
    markAllAsRead: ReturnType<typeof vi.fn>;
  };
  let mockRouter: { navigate: ReturnType<typeof vi.fn> };
  let mockSnackBar: { open: ReturnType<typeof vi.fn> };

  const mockNotifications: NotificationResponse[] = [
    {
      id: 'n1',
      recipientUserId: 'u1',
      title: 'Order Confirmed',
      message: 'Your order #FK-001 has been confirmed.',
      notificationType: 'OrderCreated',
      isRead: false,
      createdAtUtc: new Date().toISOString(),
      relatedOrderId: 'o1'
    },
    {
      id: 'n2',
      recipientUserId: 'u1',
      title: 'Order Dispatched',
      message: 'Order #FK-001 dispatched.',
      notificationType: 'OrderDispatched',
      isRead: true,
      createdAtUtc: new Date().toISOString(),
      relatedOrderId: 'o1'
    }
  ];

  beforeEach(async () => {
    mockNotificationService = {
      getNotifications: vi.fn().mockReturnValue(of(mockNotifications)),
      markAsRead: vi.fn().mockReturnValue(of({ ...mockNotifications[0], isRead: true })),
      markAllAsRead: vi.fn().mockReturnValue(of(void 0))
    };
    mockRouter = { navigate: vi.fn() };
    mockSnackBar = { open: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [CustomerNotificationsComponent],
      providers: [
        { provide: NotificationService, useValue: mockNotificationService },
        { provide: Router, useValue: mockRouter },
        { provide: MatSnackBar, useValue: mockSnackBar }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerNotificationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load notifications on init', () => {
    expect(component).toBeTruthy();
    expect(mockNotificationService.getNotifications).toHaveBeenCalled();
    expect(component.notifications().length).toBe(2);
    expect(component.unreadCount()).toBe(1);
    expect(component.loading()).toBe(false);
  });

  it('should mark single notification as read and decrement unread counter', () => {
    component.markAsRead(mockNotifications[0]);

    expect(mockNotificationService.markAsRead).toHaveBeenCalledWith('n1');
    expect(component.unreadCount()).toBe(0);
  });

  it('should mark all notifications as read', () => {
    component.markAllAsRead();

    expect(mockNotificationService.markAllAsRead).toHaveBeenCalled();
    expect(component.unreadCount()).toBe(0);
    expect(component.notifications().every(n => n.isRead)).toBe(true);
  });

  it('should navigate to target order when clicked', () => {
    component.navigateToTarget(mockNotifications[0]);

    expect(mockRouter.navigate).toHaveBeenCalledWith(['/customer/orders', 'o1']);
  });
});
