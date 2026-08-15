import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OrderReviewDialogComponent, OrderReviewDialogData } from './order-review-dialog.component';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { OrderReviewService } from '../../core/services/order-review.service';
import { of } from 'rxjs';

describe('OrderReviewDialogComponent', () => {
  let component: OrderReviewDialogComponent;
  let fixture: ComponentFixture<OrderReviewDialogComponent>;
  let mockReviewService: jasmine.SpyObj<OrderReviewService>;
  let mockDialogRef: jasmine.SpyObj<MatDialogRef<OrderReviewDialogComponent>>;

  const mockDialogData: OrderReviewDialogData = {
    orderId: 'order-123',
    orderNumber: 'FK-2026-0001',
    farmerName: 'Ramesh Farmer',
    cropName: 'Basmati Rice',
    existingReview: null
  };

  beforeEach(async () => {
    mockReviewService = jasmine.createSpyObj('OrderReviewService', ['createOrderReview', 'updateOrderReview']);
    mockDialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [OrderReviewDialogComponent],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: mockDialogData },
        { provide: MatDialogRef, useValue: mockDialogRef },
        { provide: OrderReviewService, useValue: mockReviewService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(OrderReviewDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and set default rating to 5', () => {
    expect(component).toBeTruthy();
    expect(component.rating()).toBe(5);
  });

  it('should submit review successfully', () => {
    mockReviewService.createOrderReview.and.returnValue(of({
      reviewId: 'rev-1',
      orderId: 'order-123',
      orderNumber: 'FK-2026-0001',
      customerName: 'Archi',
      farmerName: 'Ramesh',
      cropName: 'Rice',
      rating: 5,
      comment: 'Super crop!',
      createdAtUtc: new Date().toISOString()
    }));

    component.comment.set('Super crop!');
    component.submitReview();

    expect(mockReviewService.createOrderReview).toHaveBeenCalledWith('order-123', { rating: 5, comment: 'Super crop!' });
    expect(mockDialogRef.close).toHaveBeenCalled();
  });
});
