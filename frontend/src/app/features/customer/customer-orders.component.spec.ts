import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CustomerOrdersComponent } from './customer-orders.component';
import { CustomerOrderService } from './customer-order.service';
import { of, throwError } from 'rxjs';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { CustomerOrderListItem } from '../../core/models/customer-auction.models';

describe('CustomerOrdersComponent', () => {
  let component: CustomerOrdersComponent;
  let fixture: ComponentFixture<CustomerOrdersComponent>;
  let mockOrderService: any;
  let lastFilterPassed: any = null;
  let returnOrders = true;

  const mockOrders: CustomerOrderListItem[] = [
    {
      orderId: 'ord-111',
      orderNumber: 'FK-20260815-0001',
      auctionId: 'auc-111',
      cropId: 'crop-111',
      cropName: 'Wheat',
      cropType: 'Grain',
      primaryImageUrl: 'http://localhost/wheat.jpg',
      allocatedQuantityKg: 250,
      allocatedQuantityMan: 12.5,
      pricePerMan: 600,
      totalAmount: 7500,
      farmerName: 'Ramesh Farmer',
      status: 'CONFIRMED',
      paymentStatus: 'PAID',
      createdAtUtc: '2026-08-15T10:00:00Z'
    }
  ];

  beforeEach(async () => {
    lastFilterPassed = null;
    returnOrders = true;

    mockOrderService = {
      getCustomerOrders: (filter?: any) => {
        lastFilterPassed = filter;
        if (!returnOrders) {
          return throwError(() => new Error('API Error'));
        }
        return of(mockOrders);
      },
      resolveImageUrl: (url: string) => url
    };

    await TestBed.configureTestingModule({
      imports: [CustomerOrdersComponent],
      providers: [
        { provide: CustomerOrderService, useValue: mockOrderService },
        provideRouter([]),
        provideNoopAnimations()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerOrdersComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load orders on init', () => {
    expect(component).toBeTruthy();
    expect(component.orders().length).toBe(1);
    expect(component.orders()[0].orderNumber).toBe('FK-20260815-0001');
  });

  it('should render order cards with order number, crop name, price per Man, and total amount', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('FK-20260815-0001');
    expect(compiled.textContent).toContain('Wheat');
    expect(compiled.textContent).toContain('250 Kg');
    expect(compiled.textContent).toContain('600');
    expect(compiled.textContent).toContain('7,500');
    expect(compiled.textContent).toContain('Ramesh Farmer');
  });

  it('should trigger search filter', () => {
    component.onSearchChange('Wheat');
    expect(component.searchQuery()).toBe('Wheat');
    expect(lastFilterPassed?.search).toBe('Wheat');
  });

  it('should trigger status filter', () => {
    component.onStatusChange('CONFIRMED');
    expect(component.selectedStatus()).toBe('CONFIRMED');
    expect(lastFilterPassed?.status).toBe('CONFIRMED');
  });

  it('should trigger sorting change', () => {
    component.onSortChange('oldest');
    expect(component.selectedSortBy()).toBe('oldest');
    expect(lastFilterPassed?.sortBy).toBe('oldest');
  });

  it('should display empty state when no orders returned', () => {
    mockOrderService.getCustomerOrders = () => of([]);
    component.loadOrders();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('No orders yet');
    expect(compiled.textContent).toContain('Browse Auctions');
  });

  it('should display error state when service fails', () => {
    returnOrders = false;
    component.loadOrders();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load your orders.');
  });
});
