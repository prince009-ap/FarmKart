import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FarmerReviewsComponent } from './farmer-reviews.component';
import { OrderReviewService } from '../../core/services/order-review.service';
import { of } from 'rxjs';
import { RouterTestingModule } from '@angular/router/testing';

describe('FarmerReviewsComponent', () => {
  let component: FarmerReviewsComponent;
  let fixture: ComponentFixture<FarmerReviewsComponent>;
  let mockReviewService: jasmine.SpyObj<OrderReviewService>;

  beforeEach(async () => {
    mockReviewService = jasmine.createSpyObj('OrderReviewService', ['getFarmerRatingSummary']);
    mockReviewService.getFarmerRatingSummary.and.returnValue(of({
      averageRating: 4.5,
      totalReviews: 10,
      recentReviews: []
    }));

    await TestBed.configureTestingModule({
      imports: [FarmerReviewsComponent, RouterTestingModule],
      providers: [
        { provide: OrderReviewService, useValue: mockReviewService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerReviewsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load farmer reviews on init', () => {
    expect(mockReviewService.getFarmerRatingSummary).toHaveBeenCalled();
    expect(component.summary()?.averageRating).toBe(4.5);
  });
});
