import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FarmerOrdersComponent } from './farmer-orders.component';
import { FarmerOrderService } from './farmer-order.service';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { FarmerOrderListItem, FarmerOrderSummary } from '../../core/models/farmer-order.models';

describe('FarmerOrdersComponent', () => {
  let component: FarmerOrdersComponent;
  let fixture: ComponentFixture<FarmerOrdersComponent>;
  let farmerOrderServiceMock: any;

  const mockSummary: FarmerOrderSummary = {
    totalOrders: 2,
    confirmedOrdersCount: 2,
    readyForPickupCount: 0,
    pickedUpCount: 0,
    deliveredCount: 0,
    completedCount: 0
  };

  const mockOrders: FarmerOrderListItem[] = [
    {
      orderId: 'ord-1',
      orderNumber: 'FK-20260815-0001',
      auctionId: 'auc-1',
      cropId: 'crop-1',
      cropName: 'Golden Wheat',
      cropType: 'Grain',
      customerName: 'Archi Vasoya',
      allocatedQuantityKg: 250,
      allocatedQuantityMan: 12.5,
      pricePerMan: 600,
      totalAmount: 7500,
      status: 'CONFIRMED',
      fulfillmentMode: 'DELIVERY',
      paymentStatus: 'PAID',
      createdAtUtc: new Date().toISOString()
    },
    {
      orderId: 'ord-2',
      orderNumber: 'FK-20260815-0002',
      auctionId: 'auc-1',
      cropId: 'crop-1',
      cropName: 'Golden Wheat',
      cropType: 'Grain',
      customerName: 'Customer B',
      allocatedQuantityKg: 100,
      allocatedQuantityMan: 5,
      pricePerMan: 620,
      totalAmount: 3100,
      status: 'CONFIRMED',
      fulfillmentMode: 'DELIVERY',
      paymentStatus: 'PAID',
      createdAtUtc: new Date().toISOString()
    }
  ];

  beforeEach(async () => {
    farmerOrderServiceMock = {
      getFarmerOrderSummary: () => of(mockSummary),
      getFarmerOrders: (filter?: any) => of(mockOrders)
    };

    await TestBed.configureTestingModule({
      imports: [FarmerOrdersComponent],
      providers: [
        provideRouter([]),
        { provide: FarmerOrderService, useValue: farmerOrderServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerOrdersComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and render farmer orders list and metrics', () => {
    expect(component).toBeTruthy();
    expect(component.orders().length).toBe(2);

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('My Orders');
    expect(text).toContain('FK-20260815-0001');
    expect(text).toContain('Archi Vasoya');
    expect(text).toContain('Customer B');
  });

  it('should render empty state when farmer has no orders', () => {
    farmerOrderServiceMock.getFarmerOrders = () => of([]);
    component.loadOrders();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('No orders found');
    expect(text).toContain('Orders matching your search/filters will appear here.');
  });
});
