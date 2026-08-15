import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FarmerOrderDetailComponent } from './farmer-order-detail.component';
import { FarmerOrderService } from './farmer-order.service';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { FarmerOrderDetail } from '../../core/models/farmer-order.models';

describe('FarmerOrderDetailComponent', () => {
  let component: FarmerOrderDetailComponent;
  let fixture: ComponentFixture<FarmerOrderDetailComponent>;
  let farmerOrderServiceMock: any;

  const mockDetail: FarmerOrderDetail = {
    orderId: 'ord-123',
    orderNumber: 'FK-20260815-0001',
    auctionId: 'auc-1',
    cropId: 'crop-1',
    cropName: 'Sharbati Wheat',
    cropType: 'Grain',
    variety: 'Sharbati',
    customerName: 'Archi Vasoya',
    customerPhone: '9876543210',
    customerCity: 'Ahmedabad',
    customerState: 'Gujarat',
    requestedQuantityKg: 300,
    requestedQuantityMan: 15,
    allocatedQuantityKg: 250,
    allocatedQuantityMan: 12.5,
    pricePerMan: 600,
    totalAmount: 7500,
    auctionQuantityKg: 500,
    auctionQuantityMan: 25,
    winningBidAmountPerMan: 600,
    auctionStartTimeUtc: new Date().toISOString(),
    auctionEndTimeUtc: new Date().toISOString(),
    status: 'CONFIRMED',
    paymentStatus: 'PAID',
    orderDateUtc: new Date().toISOString(),
    auctionAllocationId: 'alloc-1',
    auctionPaymentId: 'pay-1',
    transactionReference: 'FK-TEST-123456',
    paymentMethod: 'CARD',
    paidAtUtc: new Date().toISOString(),
    fulfillmentMode: 'DELIVERY',
    timeline: []
  };

  beforeEach(async () => {
    farmerOrderServiceMock = {
      getFarmerOrderById: (id: string) => of(mockDetail)
    };

    await TestBed.configureTestingModule({
      imports: [FarmerOrderDetailComponent],
      providers: [
        { provide: FarmerOrderService, useValue: farmerOrderServiceMock },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'id' ? 'ord-123' : null)
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerOrderDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load farmer order details', () => {
    expect(component).toBeTruthy();
    expect(component.order()?.orderNumber).toBe('FK-20260815-0001');

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Order #FK-20260815-0001');
    expect(text).toContain('Sharbati Wheat');
    expect(text).toContain('Archi Vasoya');
    expect(text).toContain('250 Kg');
    expect(text).toContain('7,500');
  });
});
