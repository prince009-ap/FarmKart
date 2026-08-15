import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CustomerReviewsComponent } from './customer-reviews.component';
import { OrderReviewService } from '../../core/services/order-review.service';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';

describe('CustomerReviewsComponent', () => {
  let component: CustomerReviewsComponent;
  let fixture: ComponentFixture<CustomerReviewsComponent>;
  let mockReviewService: jasmine.SpyObj<OrderReviewService>;
  let mockDialog: jasmine.SpyObj<MatDialog>;

  beforeEach(async () => {
    mockReviewService = jasmine.createSpyObj('OrderReviewService', ['getMyCustomerReviews']);
    mockDialog = jasmine.createSpyObj('MatDialog', ['open']);

    mockReviewService.getMyCustomerReviews.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [CustomerReviewsComponent],
      providers: [
        { provide: OrderReviewService, useValue: mockReviewService },
        { provide: MatDialog, useValue: mockDialog }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerReviewsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load reviews on init', () => {
    expect(mockReviewService.getMyCustomerReviews).toHaveBeenCalled();
  });
});
