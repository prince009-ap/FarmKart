import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FarmerNotificationsComponent } from './farmer-notifications.component';
import { NotificationService } from '../../core/services/notification.service';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { NotificationResponse } from '../../core/models/notification.models';
import { vi, describe, beforeEach, it, expect } from 'vitest';

describe('FarmerNotificationsComponent', () => {
  let component: FarmerNotificationsComponent;
  let fixture: ComponentFixture<FarmerNotificationsComponent>;
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
      title: 'Order Paid & Confirmed',
      message: 'Order #FK-001 has been paid.',
      notificationType: 'AuctionOrderCreated',
      isRead: false,
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
      imports: [FarmerNotificationsComponent],
      providers: [
        { provide: NotificationService, useValue: mockNotificationService },
        { provide: Router, useValue: mockRouter },
        { provide: MatSnackBar, useValue: mockSnackBar }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerNotificationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load farmer notifications', () => {
    expect(component).toBeTruthy();
    expect(component.notifications().length).toBe(1);
    expect(component.unreadCount()).toBe(1);
  });
});
