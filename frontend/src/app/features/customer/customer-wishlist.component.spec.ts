import { ComponentFixture, TestBed } from '@angular/core';
import { CustomerWishlistComponent } from './customer-wishlist.component';
import { WishlistService } from '../../core/services/wishlist.service';
import { of } from 'rxjs';
import { RouterTestingModule } from '@angular/router/testing';

describe('CustomerWishlistComponent', () => {
  let component: CustomerWishlistComponent;
  let fixture: ComponentFixture<CustomerWishlistComponent>;
  let mockWishlistService: jasmine.SpyObj<WishlistService>;

  beforeEach(async () => {
    mockWishlistService = jasmine.createSpyObj('WishlistService', ['getWishlist', 'getCount', 'removeItem', 'addItem']);
    mockWishlistService.getWishlist.and.returnValue(of([]));
    mockWishlistService.getCount.and.returnValue(of({ total: 0, cropCount: 0, auctionCount: 0 }));

    await TestBed.configureTestingModule({
      imports: [CustomerWishlistComponent, RouterTestingModule],
      providers: [
        { provide: WishlistService, useValue: mockWishlistService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerWishlistComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load wishlist items on init', () => {
    expect(mockWishlistService.getWishlist).toHaveBeenCalled();
    expect(mockWishlistService.getCount).toHaveBeenCalled();
  });
});
