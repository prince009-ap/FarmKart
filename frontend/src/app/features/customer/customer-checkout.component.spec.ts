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
    quantityMan: 15,
    startingBidPrice: 500,
    currentHighestBid: 600,
    minimumBidIncrement: 20,
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
    quantityMan: 15,
    totalAuctionQuantityKg: 300,
    totalAllocatedQuantityKg: 300,
    remainingQuantityKg: 0,
    auctionStatus: 'ENDED',
    hasWinner: true,
    winningBidAmount: 600,
    winnerCustomerName: 'Winning Customer',
    totalBids: 5,
    allocations: [{
      allocationId: 'alloc-1',
      auctionId: 'auc-123',
      bidId: 'bid-1',
      customerProfileId: 'cust-1',
      customerName: 'Winning Customer',
      requestedQuantityKg: 300,
      allocatedQuantityKg: 300,
      requestedQuantityMan: 15,
      allocatedQuantityMan: 15,
      winningBidAmountPerMan: 600,
      totalPayableAmount: 9000,
      status: 'WON',
      finalizedAtUtc: '2026-08-14T05:00:00Z'
    }],
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
    quantityMan: 15,
    allocatedQuantityKg: 300,
    allocatedQuantityMan: 15,
    winningBidAmount: 600,
    totalPayableAmount: 9000,
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
    expect(component.calculateTotalPayable()).toBe(9000);
  });

  it('should process payment and set completedPayment signal', () => {
    component.payNow();
    expect(component.completedPayment()).toEqual(mockPayment);
  });
});
