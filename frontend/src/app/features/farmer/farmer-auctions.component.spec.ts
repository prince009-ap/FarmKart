import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { FarmerAuctionsComponent } from './farmer-auctions.component';
import { FarmerAuctionService } from './farmer-auction.service';

describe('FarmerAuctionsComponent', () => {
  let fixture: ComponentFixture<FarmerAuctionsComponent>;
  let auctionServiceMock: any;

  const mockAuctions = [
    {
      id: 'auc-1',
      cropId: 'crop-1',
      cropName: 'Wheat',
      variety: 'Sharbati',
      quantity: 500,
      unit: 'Kg',
      quantityKg: 500,
      quantityMan: 25,
      availableStockKg: 1000,
      reservedStockKg: 500,
      remainingUnreservedStockKg: 500,
      startingBidPrice: 500,
      minimumBidIncrement: 20,
      startTimeUtc: new Date(Date.now() - 3600000).toISOString(),
      endTimeUtc: new Date(Date.now() + 7200000).toISOString(),
      status: 'LIVE',
      totalBids: 5,
      currentHighestBid: 620,
      totalRequestedQuantityKg: 650,
      totalRequestedQuantityMan: 32.5,
      demandPercentage: 130,
      createdAtUtc: new Date().toISOString(),
      serverTimeUtc: new Date().toISOString()
    },
    {
      id: 'auc-2',
      cropId: 'crop-2',
      cropName: 'Rice',
      variety: 'Basmati',
      quantity: 300,
      unit: 'Kg',
      quantityKg: 300,
      quantityMan: 15,
      availableStockKg: 600,
      reservedStockKg: 300,
      remainingUnreservedStockKg: 300,
      startingBidPrice: 400,
      minimumBidIncrement: 15,
      startTimeUtc: new Date(Date.now() + 3600000).toISOString(),
      endTimeUtc: new Date(Date.now() + 14400000).toISOString(),
      status: 'SCHEDULED',
      totalBids: 0,
      currentHighestBid: 400,
      totalRequestedQuantityKg: 0,
      totalRequestedQuantityMan: 0,
      demandPercentage: 0,
      createdAtUtc: new Date().toISOString(),
      serverTimeUtc: new Date().toISOString()
    }
  ];

  beforeEach(async () => {
    auctionServiceMock = {
      getAuctions: () => of(mockAuctions),
      getSummaryCounts: () => of({
        totalAuctions: 2,
        upcomingCount: 1,
        liveCount: 1,
        endedCount: 0,
        cancelledCount: 0
      })
    };

    await TestBed.configureTestingModule({
      imports: [FarmerAuctionsComponent],
      providers: [
        provideRouter([]),
        { provide: FarmerAuctionService, useValue: auctionServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerAuctionsComponent);
  });

  it('renders My Auctions page and summary cards', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('My Auctions');
    expect(fixture.nativeElement.textContent).toContain('Wheat');
    expect(fixture.nativeElement.textContent).toContain('Rice');
    expect(fixture.nativeElement.textContent).toContain('LIVE');
    expect(fixture.nativeElement.textContent).toContain('UPCOMING');
  });

  it('filters auctions by status tab', () => {
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.setFilter('LIVE');
    fixture.detectChanges();
    expect(comp.filteredAuctions().length).toBe(1);
    expect(comp.filteredAuctions()[0].cropName).toBe('Wheat');

    comp.setFilter('UPCOMING');
    fixture.detectChanges();
    expect(comp.filteredAuctions().length).toBe(1);
    expect(comp.filteredAuctions()[0].cropName).toBe('Rice');
  });

  it('handles error state gracefully', () => {
    auctionServiceMock.getAuctions = () => throwError(() => new Error('Server error'));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Unable to load your auctions');
  });
});
