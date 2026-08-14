import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CustomerCheckoutComponent } from './customer-checkout.component';
import { CustomerAuctionService } from './customer-auction.service';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuctionPayment, AuctionResult, CustomerAuction } from '../../core/models/customer-auction.models';

describe('CustomerCheckoutComponent', () => {
  let component: CustomerCheckoutComponent;
  let fixture: ComponentFixture<CustomerCheckoutComponent>;
  let auctionServiceMock: any;

  const mockAuction: CustomerAuction = {
    id: 'auc-123',
    cropId: 'crop-1',
    cropName: 'Wheat',
    cropType: 'Grain',
    quantity: 300,
    unit: 'Kg',
    quantityKg: 300,
    startingBidPrice: 25,
    currentHighestBid: 31,
    minimumBidIncrement: 2,
    farmerName: 'Prince Patel',
    farmLocation: 'Surat',
    startTimeUtc: '2026-08-14T00:00:00Z',
    endTimeUtc: '2026-08-14T05:00:00Z',
    status: 'ENDED',
    primaryImageUrl: '/img.jpg',
    images: [],
    createdAtUtc: '2026-08-13T00:00:00Z',
    serverTimeUtc: '2026-08-14T06:00:00Z'
  };

  const mockResult: AuctionResult = {
    auctionId: 'auc-123',
    cropId: 'crop-1',
    cropName: 'Wheat',
    cropType: 'Grain',
    quantity: 300,
    unit: 'Kg',
    auctionStatus: 'ENDED',
    hasWinner: true,
    winningBidAmount: 31,
    winnerCustomerName: 'Winning Customer',
    totalBids: 5,
    startTimeUtc: '2026-08-14T00:00:00Z',
    endTimeUtc: '2026-08-14T05:00:00Z',
    customerResultStatus: 'WON',
    serverTimeUtc: '2026-08-14T06:00:00Z'
  };

  const mockPayment: AuctionPayment = {
    paymentId: 'pay-1',
    auctionId: 'auc-123',
    cropId: 'crop-1',
    cropName: 'Wheat',
    cropType: 'Grain',
    quantity: 300,
    unit: 'Kg',
    winningBidAmount: 31,
    totalPayableAmount: 9300,
    currency: 'INR',
    paymentMethod: 'UPI',
    paymentStatus: 'PAID',
    transactionReference: 'FK-TEST-20260814-1234',
    winnerCustomerName: 'Winning Customer',
    farmerName: 'Prince Patel',
    createdAtUtc: '2026-08-14T06:00:00Z',
    paidAtUtc: '2026-08-14T06:00:00Z',
    serverTimeUtc: '2026-08-14T06:00:00Z'
  };

  beforeEach(async () => {
    auctionServiceMock = {
      getAuctionById: (id: string) => of(mockAuction),
      getAuctionResult: (id: string) => of(mockResult),
      processAuctionPayment: (id: string, method: string) => of(mockPayment)
    };

    await TestBed.configureTestingModule({
      imports: [CustomerCheckoutComponent],
      providers: [
        provideRouter([]),
        { provide: CustomerAuctionService, useValue: auctionServiceMock },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => key === 'id' ? 'auc-123' : null
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerCheckoutComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load checkout details for winner', () => {
    expect(component).toBeTruthy();
    expect(component.auction()).toEqual(mockAuction);
    expect(component.auctionResult()).toEqual(mockResult);
    expect(component.calculateTotalPayable()).toBe(9300);
  });

  it('should process payment and set completedPayment signal', () => {
    component.payNow();
    expect(component.completedPayment()).toEqual(mockPayment);
  });
});
