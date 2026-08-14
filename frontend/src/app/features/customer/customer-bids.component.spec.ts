import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CustomerBidsComponent } from './customer-bids.component';
import { CustomerAuctionService } from './customer-auction.service';
import { CustomerMyBid } from '../../core/models/customer-auction.models';

describe('CustomerBidsComponent', () => {
  let fixture: ComponentFixture<CustomerBidsComponent>;
  let auctionServiceMock: any;

  const mockMyBids: CustomerMyBid[] = [
    {
      bidId: 'bid-1',
      auctionId: 'auc-1',
      cropId: 'crop-1',
      cropName: 'Golden Wheat',
      primaryImageUrl: 'http://example.com/wheat.jpg',
      cropType: 'Grain',
      quantity: 300,
      unit: 'Kg',
      quantityMan: 15,
      customerBidAmount: 660,
      currentHighestBid: 660,
      minimumBidIncrement: 20,
      auctionStatus: 'ENDED',
      customerBidStatus: 'WON',
      bidTimeUtc: new Date().toISOString(),
      startTimeUtc: new Date(Date.now() - 36000000).toISOString(),
      endTimeUtc: new Date(Date.now() - 360000).toISOString(),
      serverTimeUtc: new Date().toISOString()
    },
    {
      bidId: 'bid-2',
      auctionId: 'auc-2',
      cropId: 'crop-2',
      cropName: 'Organic Corn',
      primaryImageUrl: 'http://example.com/corn.jpg',
      cropType: 'Grain',
      quantity: 500,
      unit: 'Kg',
      quantityMan: 25,
      customerBidAmount: 500,
      currentHighestBid: 580,
      minimumBidIncrement: 20,
      auctionStatus: 'ENDED',
      customerBidStatus: 'LOST',
      bidTimeUtc: new Date().toISOString(),
      startTimeUtc: new Date(Date.now() - 36000000).toISOString(),
      endTimeUtc: new Date(Date.now() - 360000).toISOString(),
      serverTimeUtc: new Date().toISOString()
    }
  ];

  beforeEach(async () => {
    auctionServiceMock = {
      getMyBids: () => of(mockMyBids)
    };

    await TestBed.configureTestingModule({
      imports: [CustomerBidsComponent],
      providers: [
        provideRouter([]),
        { provide: CustomerAuctionService, useValue: auctionServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerBidsComponent);
  });

  it('renders my bids list with WON and LOST badges', () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Golden Wheat');
    expect(fixture.nativeElement.textContent).toContain('Organic Corn');
    expect(fixture.nativeElement.textContent).toContain('🏆 WON');
    expect(fixture.nativeElement.textContent).toContain('LOST');
  });
});
