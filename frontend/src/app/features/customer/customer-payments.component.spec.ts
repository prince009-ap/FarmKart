import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CustomerPaymentsComponent } from './customer-payments.component';
import { CustomerAuctionService } from './customer-auction.service';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CustomerPaymentHistory } from '../../core/models/customer-auction.models';

describe('CustomerPaymentsComponent', () => {
  let component: CustomerPaymentsComponent;
  let fixture: ComponentFixture<CustomerPaymentsComponent>;
  let auctionServiceMock: any;

  const mockPayments: CustomerPaymentHistory[] = [
    {
      paymentId: 'pay-1',
      auctionId: 'auc-123',
      cropId: 'crop-1',
      cropName: 'Wheat',
      primaryImageUrl: '/img.jpg',
      cropType: 'Grain',
      quantity: 300,
      unit: 'Kg',
      quantityMan: 15,
      winningBidAmount: 600,
      totalPayableAmount: 9000,
      currency: 'INR',
      paymentMethod: 'UPI',
      paymentStatus: 'PAID',
      transactionReference: 'FK-TEST-20260814-1234',
      createdAtUtc: '2026-08-14T06:00:00Z',
      paidAtUtc: '2026-08-14T06:00:00Z'
    }
  ];

  beforeEach(async () => {
    auctionServiceMock = {
      getPaymentHistory: () => of(mockPayments)
    };

    await TestBed.configureTestingModule({
      imports: [CustomerPaymentsComponent],
      providers: [
        provideRouter([]),
        { provide: CustomerAuctionService, useValue: auctionServiceMock },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => null } } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerPaymentsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load payment history', () => {
    expect(component).toBeTruthy();
    expect(component.payments().length).toBe(1);
    expect(component.payments()[0].totalPayableAmount).toBe(9000);
    expect(component.payments()[0].paymentStatus).toBe('PAID');
  });
});
