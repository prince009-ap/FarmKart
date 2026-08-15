import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { FarmerAuctionDetailComponent } from './farmer-auction-detail.component';
import { FarmerAuctionService } from './farmer-auction.service';

describe('FarmerAuctionDetailComponent', () => {
  let fixture: ComponentFixture<FarmerAuctionDetailComponent>;
  let auctionServiceMock: any;

  const mockAuction = {
    id: 'auc-123',
    cropId: 'crop-1',
    cropName: 'Cotton',
    variety: 'Bt Cotton',
    quantity: 500,
    unit: 'Kg',
    quantityKg: 500,
    quantityMan: 25,
    availableStockKg: 1000,
    reservedStockKg: 500,
    remainingUnreservedStockKg: 500,
    startingBidPrice: 600,
    minimumBidIncrement: 20,
    startTimeUtc: new Date().toISOString(),
    endTimeUtc: new Date(Date.now() + 3600000).toISOString(),
    status: 'LIVE',
    totalBids: 8,
    currentHighestBid: 680,
    totalRequestedQuantityKg: 650,
    totalRequestedQuantityMan: 32.5,
    demandPercentage: 130,
    createdAtUtc: new Date().toISOString(),
    serverTimeUtc: new Date().toISOString()
  };

  beforeEach(async () => {
    auctionServiceMock = {
      getAuction: () => of(mockAuction),
      getAuctionResult: () => of({ allocations: [] }),
      getAuctionPayment: () => of(null)
    };

    await TestBed.configureTestingModule({
      imports: [FarmerAuctionDetailComponent],
      providers: [
        provideRouter([]),
        { provide: FarmerAuctionService, useValue: auctionServiceMock },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: new Map([['id', 'auc-123']]) }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerAuctionDetailComponent);
  });

  it('loads and renders auction details', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Cotton');
    expect(fixture.nativeElement.textContent).toContain('Bt Cotton');
    expect(fixture.nativeElement.textContent).toContain('₹680 / Man');
  });
});
