import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CustomerOrderTrackingComponent } from './customer-order-tracking.component';
import { CustomerOrderService } from './customer-order.service';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CustomerOrderTracking } from '../../core/models/customer-auction.models';
import { vi, describe, beforeEach, it, expect } from 'vitest';

describe('CustomerOrderTrackingComponent', () => {
  let component: CustomerOrderTrackingComponent;
  let fixture: ComponentFixture<CustomerOrderTrackingComponent>;
  let mockOrderService: { getOrderTracking: ReturnType<typeof vi.fn> };

  const mockTrackingData: CustomerOrderTracking = {
    orderId: 'ord-123',
    orderNumber: 'FK-20260815-0001',
    auctionId: 'auc-123',
    cropName: 'Basmati Rice',
    cropType: 'Grain',
    variety: 'Super Fine',
    primaryImageUrl: 'http://example.com/rice.jpg',
    quantityKg: 200,
    quantityMan: 10,
    fulfillmentMode: 'DELIVERY',
    currentStatus: 'DISPATCHED',
    statusMessage: 'Your order is on its way.',
    farmerName: 'Ramesh Patel',
    farmLocation: 'Surat, Gujarat',
    deliveryAddress: '123 Ring Road',
    deliveryCity: 'Ahmedabad',
    deliveryState: 'Gujarat',
    deliveryPincode: '380001',
    contactName: 'Archi Customer',
    contactPhone: '9876543210',
    pickupLocation: null,
    pickupDate: null,
    expectedDeliveryDate: '2026-08-20T00:00:00Z',
    orderDateUtc: '2026-08-15T12:00:00Z',
    statusHistory: [
      {
        historyId: 'h1',
        previousStatus: 'CONFIRMED',
        newStatus: 'READY_FOR_PICKUP',
        changedAtUtc: '2026-08-15T13:00:00Z',
        changedByUserId: 'farmer-1'
      },
      {
        historyId: 'h2',
        previousStatus: 'READY_FOR_PICKUP',
        newStatus: 'DISPATCHED',
        changedAtUtc: '2026-08-15T14:00:00Z',
        changedByUserId: 'farmer-1'
      }
    ]
  };

  beforeEach(async () => {
    mockOrderService = {
      getOrderTracking: vi.fn().mockReturnValue(of(mockTrackingData))
    };

    await TestBed.configureTestingModule({
      imports: [CustomerOrderTrackingComponent],
      providers: [
        provideRouter([]),
        { provide: CustomerOrderService, useValue: mockOrderService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => key === 'id' ? 'ord-123' : null
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerOrderTrackingComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load tracking details', () => {
    expect(component).toBeTruthy();
    expect(mockOrderService.getOrderTracking).toHaveBeenCalledWith('ord-123');
    expect(component.tracking()?.orderNumber).toBe('FK-20260815-0001');
    expect(component.tracking()?.currentStatus).toBe('DISPATCHED');
    expect(component.isLoading()).toBe(false);
  });

  it('should correctly identify completed timeline steps', () => {
    expect(component.isStepCompleted('CONFIRMED')).toBe(true);
    expect(component.isStepCompleted('READY_FOR_PICKUP')).toBe(true);
    expect(component.isStepCompleted('DISPATCHED')).toBe(true);
    expect(component.isStepCompleted('DELIVERED')).toBe(false);
  });

  it('should handle error when order is not found', () => {
    mockOrderService.getOrderTracking.mockReturnValue(throwError(() => ({ status: 404 })));
    component.loadTracking('invalid-id');
    expect(component.errorMessage()).toContain('Order not found');
  });
});
