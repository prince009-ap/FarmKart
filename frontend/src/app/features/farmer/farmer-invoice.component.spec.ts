import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FarmerInvoiceComponent } from './farmer-invoice.component';
import { InvoiceService } from '../../core/services/invoice.service';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { InvoiceResponse } from '../../core/models/invoice.models';
import { vi, describe, beforeEach, it, expect } from 'vitest';

describe('FarmerInvoiceComponent', () => {
  let component: FarmerInvoiceComponent;
  let fixture: ComponentFixture<FarmerInvoiceComponent>;
  let mockInvoiceService: { getFarmerInvoice: ReturnType<typeof vi.fn> };

  const mockInvoice: InvoiceResponse = {
    invoiceId: 'inv-1',
    invoiceNumber: 'INV-20260815-0001',
    invoiceDateUtc: new Date().toISOString(),
    orderId: 'ord-1',
    orderNumber: 'FK-20260815-0001',
    orderDateUtc: new Date().toISOString(),
    paymentStatus: 'PAID',
    paymentReference: 'TXN-999',
    paidAtUtc: new Date().toISOString(),
    sellerName: 'Ramesh Farmer',
    sellerPhone: '9876543210',
    sellerLocation: 'Surat, Gujarat',
    buyerName: 'Archi Customer',
    buyerPhone: '9123456789',
    fulfillmentMode: 'DELIVERY',
    deliveryOrPickupAddress: '123 Ring Road, Surat',
    cropName: 'Basmati Rice',
    cropType: 'Grain',
    variety: 'Super Fine',
    primaryImageUrl: 'http://example.com/rice.jpg',
    quantityKg: 250,
    quantityMan: 12.5,
    pricePerMan: 600,
    subtotalAmount: 7500,
    taxAmount: 0,
    totalAmount: 7500
  };

  beforeEach(async () => {
    mockInvoiceService = {
      getFarmerInvoice: vi.fn().mockReturnValue(of(mockInvoice))
    };

    await TestBed.configureTestingModule({
      imports: [FarmerInvoiceComponent],
      providers: [
        provideRouter([]),
        { provide: InvoiceService, useValue: mockInvoiceService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => 'ord-1' } }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerInvoiceComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load farmer invoice details on init', () => {
    expect(component).toBeTruthy();
    expect(mockInvoiceService.getFarmerInvoice).toHaveBeenCalledWith('ord-1');
    expect(component.invoice()).toEqual(mockInvoice);
    expect(component.loading()).toBe(false);
  });
});
