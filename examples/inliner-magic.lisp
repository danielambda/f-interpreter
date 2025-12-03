(func compose (f g)
  (lambda (x) (f (g x))))

(func id (x)
  x)

(func curry (f)
  (lambda (x) (lambda (y) (f x y))))

((compose id ((curry plus) 10)) 5)
