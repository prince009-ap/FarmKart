import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CustomerAuctionDetailComponent } from './customer-auction-detail.component';
import { CustomerAuctionService } from './customer-auction.service';
import { CustomerAuction } from '../../core/models/customer-auction.models';

describe('CustomerAuctionDetailComponent', () => {
  let fixture: ComponentFixture<CustomerAuctionDetailComponent>;
  let auctionServiceMock: any;

  const mockAuction: CustomerAuction = {
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
    images: ['http://example.com/rice.jpg', 'http://example.com/rice2.jpg'],
    description: 'Premium quality organic rice harvest',
    createdAtUtc: new Date().toISOString(),
    serverTimeUtc: new Date().toISOString()
  };

  beforeEach(async () => {
    auctionServiceMock = {
      getAuctionById: (id: string) => id === 'auc-1' ? of(mockAuction) : throwError(() => new Error('Not found'))
    };

    await TestBed.configureTestingModule({
      imports: [CustomerAuctionDetailComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => key === 'id' ? 'auc-1' : null
              }
            }
          }
        },
        { provide: CustomerAuctionService, useValue: auctionServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerAuctionDetailComponent);
  });

  it('renders auction detail and live bidding placeholder', () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Basmati Rice');
    expect(fixture.nativeElement.textContent).toContain('Ramesh Patel');
    expect(fixture.nativeElement.textContent).toContain('Surat, Gujarat');
    expect(fixture.nativeElement.textContent).toContain('Live Bidding');
    expect(fixture.nativeElement.textContent).toContain('Bidding will be available in the next phase.');
  });

  it('displays error when auction ID is not found', () => {
    const route = TestBed.inject(ActivatedRoute);
    (route.snapshot.paramMap.get as any) = () => 'invalid-id';

    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Auction details could not be found');
  });
});
