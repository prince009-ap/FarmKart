import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CustomerOrderDetailComponent } from './customer-order-detail.component';
import { CustomerOrderService } from './customer-order.service';
import { of, throwError } from 'rxjs';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { CustomerOrderDetail } from '../../core/models/customer-auction.models';

describe('CustomerOrderDetailComponent', () => {
  let component: CustomerOrderDetailComponent;
  let fixture: ComponentFixture<CustomerOrderDetailComponent>;
  let mockOrderService: any;
  let shouldFail = false;

  const mockDetail: CustomerOrderDetail = {
    orderId: 'ord-111',
    orderNumber: 'FK-20260815-0001',
    auctionId: 'auc-111',
    cropId: 'crop-111',
    cropName: 'Organic Wheat',
    cropType: 'Grain',
    variety: 'Sharbati',
    primaryImageUrl: 'http://localhost/wheat.jpg',
    requestedQuantityKg: 300,
    requestedQuantityMan: 15,
    allocatedQuantityKg: 250,
    allocatedQuantityMan: 12.5,
    pricePerMan: 600,
    totalAmount: 7500,
    farmerName: 'Ramesh Farmer',
    farmLocation: 'Karnal, Haryana',
    status: 'CONFIRMED',
    paymentStatus: 'PAID',
    orderDateUtc: '2026-08-15T10:00:00Z',
    auctionStartTimeUtc: '2026-08-15T08:00:00Z',
    auctionEndDateUtc: '2026-08-15T09:00:00Z',
    auctionQuantityKg: 500,
    auctionQuantityMan: 25,
    winningBidAmount: 600,
    auctionAllocationId: 'alloc-111',
    auctionPaymentId: 'pay-111',
    transactionReference: 'FK-TEST-999',
    paymentMethod: 'UPI',
    fulfillmentMode: 'DELIVERY',
    timeline: []
  };

  beforeEach(async () => {
    shouldFail = false;

    mockOrderService = {
      getCustomerOrderById: (id: string) => {
        if (shouldFail) {
          return throwError(() => ({ status: 404 }));
        }
        return of(mockDetail);
      },
      resolveImageUrl: (url: string) => url
    };

    await TestBed.configureTestingModule({
      imports: [CustomerOrderDetailComponent],
      providers: [
        provideRouter([]),
        { provide: CustomerOrderService, useValue: mockOrderService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'id' ? 'ord-111' : null)
              }
            }
          }
        },
        provideNoopAnimations()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerOrderDetailComponent);
    component = fixture.componentInstance;
  });

  it('should create and load order details on init', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    expect(component.order()).toEqual(mockDetail);
  });

  it('should render detailed order information in UI', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('FK-20260815-0001');
    expect(compiled.textContent).toContain('Organic Wheat');
    expect(compiled.textContent).toContain('Sharbati');
    expect(compiled.textContent).toContain('250 Kg (12.5 Man)');
    expect(compiled.textContent).toContain('600');
    expect(compiled.textContent).toContain('7,500');
    expect(compiled.textContent).toContain('Ramesh Farmer');
    expect(compiled.textContent).toContain('FK-TEST-999');
    expect(compiled.textContent).toContain('UPI');
  });

  it('should display error message when order not found or forbidden', () => {
    shouldFail = true;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Order not found or you do not have permission to view this order.');
  });
});
