   M S B A M L   `   `   `  ����?  ;moji, Version=1.0.0.0, Culture=neutral, PublicKeyToken=nullclr-namespace:mojimoji  5   	   =#clr-namespace:Walterlv.Demo.InteropWalterlv.Demo.Interop  5   	       moji.MainWindow   5   	   W SPresentationCore, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=6.0.2.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35^ ZWindowsFormsIntegration, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35H 9http://schemas.microsoft.com/winfx/2006/xaml/presentation      5   	   R NSystem.Xaml, Version=6.0.2.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   @d2http://schemas.microsoft.com/expression/blend/2008     5   	   Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5   	   $localclr-namespace:moji     5   	   7interop#clr-namespace:Walterlv.Demo.Interop     5   	     � Title$  	Moji Dict��5	   	   $	��450��6   $	��800��6(   
������  � WindowStyle$
 None=�5   	    � AllowsTransparency$
 True��5   	    � 
ResizeMode$ 	CanResize=�5   	   &    Walterlv.Demo.Interop.WindowBlur   	IsEnabled$
 True��5   	   '  !System.Windows.Shell.WindowChrome   WindowChrome 5        5   
      GlassFrameThickness$ -1q�5         ResizeBorderThickness$ 5q�5         CaptionHeight$ 0Q�5      	 � 	Resources	 5         ModernScrollViewer
 �� 
TargetType �� Property �� Value �� 
TargetType��%�   &	        �� 5   
   "
 ��6+   .�5      ��  � �� 5      �� 5      " ��6&   .R�5      � .+�5      �� 5      5      5!      5"      5#      .��5&      ��  �� CornerRadius$ 0��6   ���	#A0202020$��0q�5(      $��0q�5*      ��5)      �� *BorderBrush+ J� Effect 5+   
   3  -System.Windows.Media.Effects.DropShadowEffect  5,         
BlurRadius$ 0Q�5-         	Direction$ 0Q�5.         ShadowDepth$ 0Q�5/         Opacity$ 0Q�50      ._�52   
   �  � RowDefinitions 53      �� 54      $��8 �6    �� 55      $��* �6    .+�59      �� -   5        $��0��59      
������ 68   �� 5<      $��1��6   $��20,12,20,20q�5=      #��[  5>      $��Hidden=�5?      $��Hidden=�5@      .��5A      �� -   5         ��Name$ ResultTextBox��5A       �� 
IsReadOnly$
 True��65   
������ $��0q�5B   6   $��Stretch=�5D      ��5C      �� *ActualWidth+ �� RelativeSource ��  �� Mode$ FindAncestor=� �� AncestorType ScrollViewer	 5E      ��%_   'J�       �� 5F      "
 J�6    .�5G      ��  �@��Margin$ 0,0,0,10q�67   5H      5I       �5J      /� 5K       /� 	PageWidth 6'   �� *ActualWidth+ �� $ FindAncestor=� ScrollViewer5N      5P      5Q      5R      5S      5T      