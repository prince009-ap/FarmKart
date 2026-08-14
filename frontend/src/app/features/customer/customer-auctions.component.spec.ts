import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CustomerAuctionsComponent } from './customer-auctions.component';
import { CustomerAuctionService } from './customer-auction.service';
import { CustomerAuction } from '../../core/models/customer-auction.models';

describe('CustomerAuctionsComponent', () => {
  let fixture: ComponentFixture<CustomerAuctionsComponent>;
  let auctionServiceMock: any;

  const mockAuctions: CustomerAuction[] = [
    {
      id: 'auc-1',
      cropId: 'crop-1',
      cropName: 'Basmati Rice',
      cropType: 'Grain',
      variety: 'Super 1121',
      quantity: 500,
      unit: 'Kg',
      quantityKg: 500,
      startingBidPrice: 40,
      currentHighestBid: 45,
      minimumBidIncrement: 5,
      farmerName: 'Ramesh Patel',
      farmLocation: 'Surat, Gujarat',
      startTimeUtc: new Date(Date.now() - 3600000).toISOString(),
      endTimeUtc: new Date(Date.now() + 7200000).toISOString(),
      status: 'LIVE',
      primaryImageUrl: 'http://example.com/rice.jpg',
      images: ['http://example.com/rice.jpg'],
      description: 'Organic premium rice',
      createdAtUtc: new Date().toISOString(),
      serverTimeUtc: new Date().toISOString()
    },
    {
      id: 'auc-2',
      cropId: 'crop-2',
      cropName: 'Fresh Tomatoes',
      cropType: 'Vegetable',
      variety: 'Hybrid Red',
      quantity: 200,
      unit: 'Kg',
      quantityKg: 200,
      startingBidPrice: 20,
      currentHighestBid: 0,
      minimumBidIncrement: 2,
      farmerName: 'Suresh Kumar',
      farmLocation: 'Karnal, Haryana',
      startTimeUtc: new Date(Date.now() + 3600000).toISOString(),
      endTimeUtc: new Date(Date.now() + 86400000).toISOString(),
      status: 'UPCOMING',
      primaryImageUrl: null,
      images: [],
      description: 'Juicy red tomatoes',
      createdAtUtc: new Date().toISOString(),
      serverTimeUtc: new Date().toISOString()
    }
  ];

  beforeEach(async () => {
    auctionServiceMock = {
      getMarketplaceAuctions: () => of(mockAuctions)
    };

    await TestBed.configureTestingModule({
      imports: [CustomerAuctionsComponent],
      providers: [
        provideRouter([]),
        { provide: CustomerAuctionService, useValue: auctionServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerAuctionsComponent);
  });

  it('renders auction marketplace and displays auction cards', () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Browse Farm Produce Auctions');
    expect(fixture.nativeElement.textContent).toContain('Basmati Rice');
    expect(fixture.nativeElement.textContent).toContain('Fresh Tomatoes');
    expect(fixture.nativeElement.textContent).toContain('LIVE');
    expect(fixture.nativeElement.textContent).toContain('UPCOMING');
  });

  it('filters auctions when search query changes', () => {
    fixture.detectChanges();

    componentInstance().onSearchChange('Tomatoes');

    expect(componentInstance().searchQuery()).toBe('Tomatoes');
  });

  it('displays empty state when no auctions match', () => {
    auctionServiceMock.getMarketplaceAuctions = () => of([]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No auctions available');
    expect(fixture.nativeElement.textContent).toContain('Check back soon for fresh produce auctions from local farmers.');
  });

  it('displays error message and retry button when API fails', () => {
    auctionServiceMock.getMarketplaceAuctions = () => throwError(() => new Error('API Error'));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Failed to load marketplace auctions');
    expect(fixture.nativeElement.textContent).toContain('Retry');
  });

  function componentInstance(): CustomerAuctionsComponent {
    return fixture.componentInstance;
  }
});
