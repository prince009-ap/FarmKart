import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CustomerAuctionBidsComponent } from './customer-auction-bids.component';
import { CustomerAuctionService } from './customer-auction.service';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('CustomerAuctionBidsComponent', () => {
  let component: CustomerAuctionBidsComponent;
  let fixture: ComponentFixture<CustomerAuctionBidsComponent>;
  let auctionServiceMock: any;

  const mockAuction = {
    id: 'auc-123',
    cropId: 'crop-1',
    cropName: 'Rice',
    cropType: 'Cereal',
    quantity: 600,
    unit: 'Kg',
    quantityKg: 600,
    quantityMan: 30,
    startingBidPrice: 500,
    currentHighestBid: 690,
    minimumBidIncrement: 10,
    farmerName: 'Prince Patel',
    farmLocation: 'Gujarat',
    startTimeUtc: '2026-08-14T10:00:00Z',
    endTimeUtc: '2026-08-14T20:00:00Z',
    status: 'ENDED',
    images: [],
    createdAtUtc: '2026-08-14T09:00:00Z',
    serverTimeUtc: '2026-08-14T21:00:00Z'
  };

  const mockBids = [
    {
      id: 'bid-1',
      auctionId: 'auc-123',
      customerProfileId: 'cust-1',
      customerName: 'Archi Vasoya',
      amount: 690,
      requestedQuantityKg: 350,
      requestedQuantityMan: 17.5,
      bidTimeUtc: '2026-08-14T11:31:34Z',
      bidStatus: 'HIGHEST BID',
      allocationStatus: 'PARTIALLY_WON'
    },
    {
      id: 'bid-2',
      auctionId: 'auc-123',
      customerProfileId: 'cust-1',
      customerName: 'Archi Vasoya',
      amount: 660,
      requestedQuantityKg: 300,
      requestedQuantityMan: 15,
      bidTimeUtc: '2026-08-14T11:31:21Z',
      bidStatus: 'VALID',
      allocationStatus: 'LOST'
    }
  ];

  beforeEach(async () => {
    auctionServiceMock = {
      getAuctionById: (id: string) => of(mockAuction),
      getAuctionBids: (id: string, sort?: string) => of(mockBids)
    };

    await TestBed.configureTestingModule({
      imports: [CustomerAuctionBidsComponent, NoopAnimationsModule],
      providers: [
        { provide: CustomerAuctionService, useValue: auctionServiceMock },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'id' ? 'auc-123' : null)
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerAuctionBidsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load bidding activity', () => {
    expect(component).toBeTruthy();
    expect(component.bids().length).toBe(2);
  });

  it('should render Bidding Activity header and bids table', () => {
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Bidding Activity — Rice');
    expect(text).toContain('Archi Vasoya');
    expect(text).toContain('PARTIALLY WON');
    expect(text).toContain('LOST');
  });
});
