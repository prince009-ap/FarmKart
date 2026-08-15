import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { FarmerAuctionBidsComponent } from './farmer-auction-bids.component';
import { FarmerAuctionService } from './farmer-auction.service';

describe('FarmerAuctionBidsComponent', () => {
  let fixture: ComponentFixture<FarmerAuctionBidsComponent>;
  let auctionServiceMock: any;

  const mockAuction = {
    id: 'auc-789',
    cropName: 'Soybean',
    quantityKg: 500,
    quantityMan: 25,
    currentHighestBid: 620,
    totalRequestedQuantityKg: 650,
    demandPercentage: 130,
    status: 'LIVE'
  };

  const mockBids = [
    {
      bidId: 'b-1',
      auctionId: 'auc-789',
      customerProfileId: 'cust-1',
      customerName: 'Customer A',
      requestedQuantityKg: 300,
      requestedQuantityMan: 15,
      bidAmountPerMan: 620,
      bidTimeUtc: new Date().toISOString(),
      bidStatus: 'Active'
    },
    {
      bidId: 'b-2',
      auctionId: 'auc-789',
      customerProfileId: 'cust-2',
      customerName: 'Customer B',
      requestedQuantityKg: 350,
      requestedQuantityMan: 17.5,
      bidAmountPerMan: 600,
      bidTimeUtc: new Date().toISOString(),
      bidStatus: 'Active'
    }
  ];

  beforeEach(async () => {
    auctionServiceMock = {
      getAuction: () => of(mockAuction),
      getAuctionBids: () => of(mockBids)
    };

    await TestBed.configureTestingModule({
      imports: [FarmerAuctionBidsComponent],
      providers: [
        provideRouter([]),
        { provide: FarmerAuctionService, useValue: auctionServiceMock },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: new Map([['id', 'auc-789']]) }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerAuctionBidsComponent);
  });

  it('renders auction bids header and customer names', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Bidding Activity — Soybean');
    expect(fixture.nativeElement.textContent).toContain('Customer A');
    expect(fixture.nativeElement.textContent).toContain('Customer B');
    expect(fixture.nativeElement.textContent).toContain('₹620 / Man');
  });
});
